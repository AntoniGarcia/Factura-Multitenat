using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Modules.Plataforma.Empresas;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Operador;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Operador.Configuracion;

public sealed class ServicioFiscalDelProveedor(
    AppDbContext db,
    IPasswordHasher<OperadorPlataforma> hasher,
    IDataProtectionProvider protecciones,
    IServicioDeBitacora bitacora)
{
    public async Task<ConfiguracionFiscalProveedorDto> ObtenerAsync(CancellationToken ct)
    {
        var c = await db.ConfiguracionFiscalProveedor.AsNoTracking().SingleOrDefaultAsync(ct);
        var certificados = await db.CertificadosProveedor.AsNoTracking()
            .OrderByDescending(x => x.Activo).ThenByDescending(x => x.CargadoUtc)
            .Select(x => new CertificadoProveedorDto(x.Id, x.NumeroSerie, x.VigenciaDesdeUtc,
                x.VigenciaHastaUtc, x.Activo, x.CargadoUtc)).ToListAsync(ct);
        return new(c?.Rfc ?? "", c?.NombreFiscal ?? "", c?.RegimenFiscal ?? "",
            c?.CodigoPostalExpedicion ?? "", c?.ZonaHoraria ?? "America/Mexico_City",
            c?.Serie ?? "", c?.Version, certificados);
    }

    public Task<List<ClaveSatDto>> RegimenesAsync(CancellationToken ct)
        => db.SatRegimenesFiscales.AsNoTracking().Where(x => x.Vigente).OrderBy(x => x.Clave)
            .Select(x => new ClaveSatDto("c_RegimenFiscal", x.Clave, x.Descripcion, x.Vigente)).ToListAsync(ct);

    public async Task<Resultado<ConfiguracionFiscalProveedorDto>> GuardarAsync(
        Guid operadorId, PeticionGuardarFiscalProveedor p, CancellationToken ct)
    {
        if (!await ConfirmarOperadorAsync(operadorId, p.ContrasenaDelOperador, ct))
            return ErrorNegocio.Validacion("contrasena-incorrecta", "Tu contraseña de operador no es correcta.");

        var rfc = (p.Rfc ?? "").Trim().ToUpperInvariant();
        var validacionRfc = Rfc.Validar(rfc);
        if (!validacionRfc.EsValido || Rfc.EsGenerico(rfc))
            return ErrorNegocio.Validacion("rfc-invalido", validacionRfc.Mensaje ?? "Escribe el RFC del proveedor.");
        var nombre = NombreFiscal.Normalizar(p.NombreFiscal).Normalizado;
        var regimen = (p.RegimenFiscal ?? "").Trim();
        var cp = (p.CodigoPostalExpedicion ?? "").Trim();
        var zona = (p.ZonaHoraria ?? "").Trim();
        var serie = (p.Serie ?? "").Trim().ToUpperInvariant();
        if (nombre.Length is 0 or > 254 || nombre.Contains('|') || nombre.Any(char.IsControl))
            return ErrorNegocio.Validacion("nombre-invalido", "Escribe una razón social válida, de hasta 254 caracteres.");
        if (serie.Length is 0 or > 25 || serie.Any(x => !char.IsAsciiLetterOrDigit(x) && x != '-'))
            return ErrorNegocio.Validacion("serie-invalida", "La serie admite hasta 25 letras, números o guiones.");
        var fiscal = await db.SatRegimenesFiscales.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Clave == regimen && x.Vigente, ct);
        if (fiscal is null || (Rfc.EsPersonaMoral(rfc) ? !fiscal.AplicaMoral : !fiscal.AplicaFisica))
            return ErrorNegocio.Validacion("regimen-incompatible", "Selecciona un régimen vigente que corresponda al RFC.");
        if (cp.Length != 5 || !cp.All(char.IsAsciiDigit)
            || !await db.SatCodigosPostales.AnyAsync(x => x.Clave == cp && x.Vigente, ct))
            return ErrorNegocio.Validacion("cp-invalido", "Selecciona un código postal vigente del catálogo SAT.");
        if (!ZonaValida(zona))
            return ErrorNegocio.Validacion("zona-invalida", "Selecciona un huso horario válido.");

        var c = await db.ConfiguracionFiscalProveedor.SingleOrDefaultAsync(ct);
        if (!VersionCoincide(c, p.Version)) return CambiosPendientes();
        if (c is not null && c.Rfc != rfc && await db.CertificadosProveedor.AnyAsync(ct))
            return ErrorNegocio.Conflicto("rfc-proveedor-fijo", "El RFC no puede cambiar después de cargar un CSD.");
        var antes = c is null ? null : DatosAuditables(c);
        if (c is null)
        {
            c = new ConfiguracionFiscalProveedor
            {
                Id = 1, Rfc = rfc, NombreFiscal = nombre, RegimenFiscal = regimen,
                CodigoPostalExpedicion = cp, ZonaHoraria = zona, Serie = serie
            };
            db.ConfiguracionFiscalProveedor.Add(c);
        }
        c.Rfc = rfc; c.NombreFiscal = nombre; c.RegimenFiscal = regimen;
        c.CodigoPostalExpedicion = cp; c.ZonaHoraria = zona; c.Serie = serie;
        c.ActualizadaUtc = DateTime.UtcNow; c.ActualizadaPorOperadorId = operadorId;
        bitacora.Registrar("ConfiguracionFiscalProveedor", "1", "fiscal_proveedor_guardada",
            antes, DatosAuditables(c), operadorId: operadorId);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return CambiosPendientes(); }
        catch (DbUpdateException ex) when (EsDuplicado(ex)) { return CambiosPendientes(); }
        return await ObtenerAsync(ct);
    }

    public async Task<Resultado<ConfiguracionFiscalProveedorDto>> CargarCsdAsync(
        Guid operadorId, byte[] cer, byte[] key, string contrasena, string contrasenaOperador,
        byte[]? version, CancellationToken ct)
    {
        if (!await ConfirmarOperadorAsync(operadorId, contrasenaOperador, ct))
            return ErrorNegocio.Validacion("contrasena-incorrecta", "Tu contraseña de operador no es correcta.");
        if (cer.Length is 0 or > 65536 || key.Length is 0 or > 65536 || contrasena.Length is 0 or > 512)
            return ErrorNegocio.Validacion("csd-incompleto", "Carga los archivos .cer y .key, de hasta 64 KB, y su contraseña.");
        var c = await db.ConfiguracionFiscalProveedor.SingleOrDefaultAsync(ct);
        if (c is null) return ErrorNegocio.Conflicto("fiscal-proveedor-pendiente", "Guarda primero los datos fiscales.");
        if (!VersionCoincide(c, version)) return CambiosPendientes();
        var leido = LectorDeCsd.Leer(cer, key, contrasena, c.Rfc);
        if (leido.EsFallo) return leido.Error!;
        var datos = leido.Valor;
        if (await db.CertificadosProveedor.AnyAsync(x => x.NumeroSerie == datos.NumeroSerie, ct))
            return ErrorNegocio.Conflicto("csd-duplicado", "Ese certificado ya está cargado.");

        var nuevo = new CertificadoProveedor
        {
            Id = Guid.NewGuid(), ConfiguracionId = c.Id, Rfc = c.Rfc, NumeroSerie = datos.NumeroSerie,
            VigenciaDesdeUtc = datos.VigenciaDesdeUtc, VigenciaHastaUtc = datos.VigenciaHastaUtc,
            CerCifrado = Protector(c.Rfc, "cer").Protect(cer),
            KeyCifrada = Protector(c.Rfc, "key").Protect(key),
            ContrasenaCifrada = Protector(c.Rfc, "contrasena").Protect(contrasena),
            Activo = true, CargadoUtc = DateTime.UtcNow, CargadoPorOperadorId = operadorId
        };
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var anteriores = await db.CertificadosProveedor.Where(x => x.Activo).ToListAsync(ct);
            foreach (var anterior in anteriores) anterior.Activo = false;
            c.ActualizadaUtc = DateTime.UtcNow; c.ActualizadaPorOperadorId = operadorId;
            // Se libera el índice de CSD activo dentro de la misma transacción.
            await db.SaveChangesAsync(ct);
            db.CertificadosProveedor.Add(nuevo);
            bitacora.Registrar("CertificadoProveedor", nuevo.Id.ToString(), "csd_proveedor_cargado",
                antes: anteriores.Select(x => new { x.NumeroSerie }),
                despues: new { nuevo.NumeroSerie, nuevo.VigenciaDesdeUtc, nuevo.VigenciaHastaUtc },
                operadorId: operadorId);
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { await transaccion.RollbackAsync(ct); return CambiosPendientes(); }
        catch (DbUpdateException ex) when (EsDuplicado(ex))
        { await transaccion.RollbackAsync(ct); return CambiosPendientes(); }
        return await ObtenerAsync(ct);
    }

    private IDataProtector Protector(string rfc, string parte)
        => protecciones.CreateProtector("Facturacion.CsdProveedor.v1", rfc, parte);

    private async Task<bool> ConfirmarOperadorAsync(Guid id, string? contrasena, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(contrasena) || contrasena.Length > 128) return false;
        var o = await db.OperadoresPlataforma.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Activo, ct);
        return o is not null && hasher.VerifyHashedPassword(o, o.HashContrasena, contrasena) != PasswordVerificationResult.Failed;
    }

    private static bool VersionCoincide(ConfiguracionFiscalProveedor? c, byte[]? version)
        => c is null ? version is null : version is { Length: 8 } && c.Version.AsSpan().SequenceEqual(version);
    private static ErrorNegocio CambiosPendientes()
        => ErrorNegocio.Conflicto("fiscal-proveedor-modificada", "La configuración cambió. Recarga la pestaña antes de guardar.");
    private static bool EsDuplicado(DbUpdateException ex)
        => ex.InnerException is SqlException { Number: 2601 or 2627 };
    private static object DatosAuditables(ConfiguracionFiscalProveedor c)
        => new { c.Rfc, c.NombreFiscal, c.RegimenFiscal, c.CodigoPostalExpedicion, c.ZonaHoraria, c.Serie };
    private static bool ZonaValida(string zona)
    {
        if (zona.Length is 0 or > 64) return false;
        try { TimeZoneInfo.FindSystemTimeZoneById(zona); return true; }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return false; }
    }
}
