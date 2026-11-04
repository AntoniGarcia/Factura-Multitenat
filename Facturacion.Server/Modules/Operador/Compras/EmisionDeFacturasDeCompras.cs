using System.Security.Cryptography;
using System.Text;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Plataforma.Empresas;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Operador.Compras;

/// <summary>Las consultas globales localizan la compra; las escrituras usan su cuenta obtenida de SQL.</summary>
public sealed class EmisionDeFacturasDeCompras(
    AppDbContext db, IContextoDeOperador operador, IHttpContextAccessor accesor,
    IDataProtectionProvider protecciones, ServicioDeFacturasDeCompras borradores,
    IServicioCatalogosSat catalogos) : IEmisionDeFacturasDeCompras
{
    public async Task<Resultado<FacturaProveedorEmitidaDto>> ObtenerEmitidaAsync(Guid compraId, CancellationToken ct)
    {
        IQueryable<FacturaDeCompra> facturas = db.FacturasDeCompras.AsNoTracking();
        IQueryable<IntentoFacturaProveedor> intentos = db.IntentosFacturasProveedor.AsNoTracking();
        if (operador.OperadorId is { } actor)
        {
            if (!await db.OperadoresPlataforma.AnyAsync(x => x.Id == actor && x.Activo, ct))
                return ErrorNegocio.SinPermiso("operador-no-activo", "Tu acceso de operador no está disponible.");
            // Solo la identidad del operador permite consultar las cuentas del proveedor.
            facturas = facturas.IgnoreQueryFilters(); intentos = intentos.IgnoreQueryFilters();
        }
        var f = await facturas.SingleOrDefaultAsync(x => x.CompraId == compraId, ct);
        if (f is null) return NoEncontrada();
        if (f.Estado != "timbrado" || f.Uuid is null)
            return ErrorNegocio.Conflicto("factura-sin-emitir", "La factura todavía no está emitida.");
        var i = await intentos.SingleOrDefaultAsync(x => x.FacturaId == f.Id && x.Estado == "timbrado" && x.Uuid == f.Uuid, ct);
        if (i is null || string.IsNullOrWhiteSpace(i.XmlTimbrado) || i.FechaTimbradoUtc is null || string.IsNullOrWhiteSpace(i.CadenaOriginalSat))
            return ErrorNegocio.Conflicto("factura-sin-archivo", "El archivo de esta factura no está disponible.");
        return new FacturaProveedorEmitidaDto(f.Id, f.CompraId, f.Uuid.Value, i.XmlTimbrado,
            f.EmisorZonaHoraria, i.FechaEmisionUtc, i.FechaTimbradoUtc.Value, i.CadenaOriginalSat);
    }

    public async Task<Resultado<IntentoFacturaCompraDto>> PrepararAsync(
        Guid compraId, byte[] version, string claveIdempotencia, CancellationToken ct)
    {
        if (operador.OperadorId is not { } actor || !await db.OperadoresPlataforma.AnyAsync(x => x.Id == actor && x.Activo, ct))
            return ErrorNegocio.SinPermiso("operador-no-activo", "Tu acceso de operador no está disponible.");
        if (version is not { Length: 8 } || string.IsNullOrWhiteSpace(claveIdempotencia) ||
            claveIdempotencia.Length > 128 || claveIdempotencia.Any(char.IsControl))
            return ErrorNegocio.Validacion("peticion-timbrado-invalida", "Revisa la versión y la clave de la solicitud.");
        var cuenta = await db.ComprasTimbres.IgnoreQueryFilters().Where(x => x.Id == compraId)
            .Select(x => (Guid?)x.CuentaId).SingleOrDefaultAsync(ct);
        if (cuenta is null) return NoEncontrada();
        await using var datos = Contexto(cuenta.Value);
        await using var tx = await datos.Database.BeginTransactionAsync(ct);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(claveIdempotencia)));
        await Bloquear(datos, $"factura-proveedor-clave:{actor:N}:{hash}", ct);
        var anterior = await datos.IntentosFacturasProveedor.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperadorId == actor && x.ClaveSolicitud == claveIdempotencia, ct);
        if (anterior is not null)
        {
            var origen = await datos.FacturasDeCompras.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(x => x.Id == anterior.FacturaId, ct);
            if (origen.CompraId != compraId || !anterior.VersionSolicitada.AsSpan().SequenceEqual(version))
                return ErrorNegocio.Regla("clave-reutilizada", "La clave ya corresponde a otra solicitud.");
            return ADto(origen, anterior, false);
        }
        await Bloquear(datos, $"factura-proveedor-compra:{compraId:N}", ct);
        var f = await datos.FacturasDeCompras.SingleOrDefaultAsync(x => x.CompraId == compraId, ct);
        if (f is null) return ErrorNegocio.Conflicto("factura-sin-guardar", "Guarda primero el borrador de la factura.");
        if (f.Estado is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto("factura-no-timbrable", "La factura ya está emitida o tiene un intento pendiente.");
        if (!f.Version.AsSpan().SequenceEqual(version)) return Modificada();
        var compra = await datos.ComprasTimbres.AsNoTracking().SingleAsync(x => x.Id == compraId, ct);
        if (ServicioDeFacturasDeCompras.ValidarCompra(compra) is { } errorCompra) return errorCompra;
        if (f.ReceptorRfc != compra.EmpresaRfcAlComprar || f.EmpresaId != compra.EmpresaId ||
            f.Subtotal != compra.Subtotal || f.Iva != compra.Iva || f.Total != compra.PrecioTotal || f.TasaIva != compra.TasaIva)
            return ErrorNegocio.Conflicto("compra-modificada", "Los importes de la compra cambiaron. Revisa la factura.");
        var error = await borradores.ValidarAsync(f.ReceptorRfc,
            new(f.ReceptorNombre, f.ReceptorRegimen, f.ReceptorCodigoPostal, f.UsoCfdi, f.Descripcion,
                f.ClaveProdServ, f.ClaveUnidad, f.FormaPago, f.Version, f.VersionProveedor), ct);
        if (error is not null) return error;
        if (await catalogos.ResolverAsync("c_CodigoPostal", f.EmisorCodigoPostal, ct) is not { Vigente: true } ||
            !await datos.SatRegimenesFiscales.AnyAsync(x => x.Clave == f.EmisorRegimen && x.Vigente &&
                (f.EmisorRfc.Length == 12 ? x.AplicaMoral : x.AplicaFisica), ct) ||
            !await datos.SatMonedas.AnyAsync(x => x.Clave == "MXN" && x.Vigente && x.Decimales == 2, ct))
            return ErrorNegocio.Regla("emisor-catalogos-invalidos", "Revisa los datos fiscales del proveedor y los catálogos.");
        // La compra desglosa desde el total con IVA incluido; multiplicar la base redondeada cambia una millonésima.
        if (f.Total <= 0 || f.Subtotal != decimal.Round(f.Total / (1m + f.TasaIva), 6, MidpointRounding.AwayFromZero) ||
            decimal.Round(f.Subtotal, 2, MidpointRounding.AwayFromZero) + decimal.Round(f.Iva, 2, MidpointRounding.AwayFromZero)
                != decimal.Round(f.Total, 2, MidpointRounding.AwayFromZero))
            return ErrorNegocio.Regla("importes-no-emisibles", "Los importes no coinciden al redondear a pesos. Revisa la compra.");
        var ahora = DateTime.UtcNow;
        var certificado = await datos.CertificadosProveedor.AsNoTracking().SingleOrDefaultAsync(x => x.Activo && x.Rfc == f.EmisorRfc, ct);
        if (certificado is null || certificado.VigenciaDesdeUtc > ahora || certificado.VigenciaHastaUtc <= ahora)
            return ErrorNegocio.Regla("csd-proveedor-no-disponible", "Carga un CSD vigente del proveedor en Configuración.");
        try
        {
            var material = Descifrar(certificado);
            try
            {
                if (LectorDeCsd.Leer(material.CertificadoCer, material.LlavePrivadaKey, material.ContrasenaLlave, f.EmisorRfc).EsFallo)
                    return ErrorNegocio.Regla("csd-proveedor-invalido", "Revisa el CSD del proveedor.");
            }
            finally { Limpiar(material); }
        }
        catch (CryptographicException) { return ErrorNegocio.Regla("csd-proveedor-no-legible", "No se pudo abrir el CSD del proveedor."); }
        var intento = new IntentoFacturaProveedor
        {
            Id = Guid.NewGuid(), CuentaId = f.CuentaId, FacturaId = f.Id, CertificadoId = certificado.Id,
            OperadorId = actor, ClaveSolicitud = claveIdempotencia, VersionSolicitada = version.ToArray(),
            Control = Guid.NewGuid(), Estado = "timbrando", FechaEmisionUtc = ahora, ActualizadaUtc = ahora
        };
        if (f.Folio is null)
        {
            var folios = await datos.Database.SqlQueryRaw<int>(
                "EXEC dbo.ReservarFolioProveedor @Rfc, @Serie",
                new SqlParameter("@Rfc", f.EmisorRfc), new SqlParameter("@Serie", f.Serie)).ToListAsync(ct);
            f.Folio = folios.Single();
        }
        intento.Folio = f.Folio.Value;
        f.Estado = "timbrando"; f.ActualizadaUtc = ahora; f.ActualizadaPorOperadorId = actor;
        datos.IntentosFacturasProveedor.Add(intento);
        Auditar(datos, f, intento, "factura_compra_preparada");
        try { await datos.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Modificada(); }
        await tx.CommitAsync(ct);
        return ADto(f, intento, true);
    }

    public async Task<Resultado<CsdDescifradoDto>> ObtenerCsdAsync(Guid intentoId, Guid control, CancellationToken ct)
    {
        var ubicacion = await Localizar(intentoId, ct);
        if (ubicacion is null) return NoEncontrada();
        await using var datos = Contexto(ubicacion.CuentaId);
        await using var tx = await datos.Database.BeginTransactionAsync(ct);
        await Bloquear(datos, $"factura-proveedor-intento:{intentoId:N}", ct);
        var intento = await datos.IntentosFacturasProveedor.SingleAsync(x => x.Id == intentoId, ct);
        if (intento.Control != control || intento.Estado != "timbrando") return Modificada();
        var f = await datos.FacturasDeCompras.SingleAsync(x => x.Id == intento.FacturaId, ct);
        var certificado = await datos.CertificadosProveedor.AsNoTracking().SingleAsync(x => x.Id == intento.CertificadoId, ct);
        var ahora = DateTime.UtcNow;
        if (certificado.Rfc != f.EmisorRfc || certificado.VigenciaDesdeUtc > ahora || certificado.VigenciaHastaUtc <= ahora)
            return ErrorNegocio.Regla("csd-no-vigente", "El CSD del intento ya no está vigente.");
        CsdDescifradoDto material;
        try { material = Descifrar(certificado); }
        catch (CryptographicException) { return ErrorNegocio.Regla("csd-no-legible", "No se pudo abrir el CSD del proveedor."); }
        try
        {
            Auditar(datos, f, intento, "csd_proveedor_leido");
            await datos.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return material;
        }
        catch { Limpiar(material); throw; }
    }

    public async Task<Resultado> GuardarXmlAsync(Guid intentoId, Guid control, XmlFacturaProveedorDto xml, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(xml.Xml) || xml.Xml.Length > 100_000 || string.IsNullOrWhiteSpace(xml.Sello))
            return ErrorNegocio.Validacion("xml-invalido", "No se pudo preparar el XML de la factura.");
        return await Cambiar(intentoId, control, async (datos, f, i) =>
        {
            var numero = await datos.CertificadosProveedor.Where(x => x.Id == i.CertificadoId).Select(x => x.NumeroSerie).SingleAsync(ct);
            if (numero != xml.NoCertificado || i.XmlEnviado is not null &&
                (i.XmlEnviado != xml.Xml || i.Sello != xml.Sello || i.CadenaOriginal != xml.CadenaOriginal || i.NoCertificado != xml.NoCertificado))
                return Modificada();
            i.XmlEnviado = xml.Xml; i.CadenaOriginal = xml.CadenaOriginal;
            i.Sello = xml.Sello; i.NoCertificado = xml.NoCertificado;
            Auditar(datos, f, i, "factura_compra_xml_preparado");
            return Resultado.Exito();
        }, ct);
    }

    public Task<Resultado> ResolverAsync(Guid intentoId, Guid control, ResolucionFacturaProveedorDto r, CancellationToken ct)
        => Cambiar(intentoId, control, (datos, f, i) =>
        {
            if (!Enum.IsDefined(r.Resultado) || r.CodigoError?.Length > 100)
                return Task.FromResult<Resultado>(ErrorNegocio.Validacion("resolucion-invalida", "La respuesta no es válida."));
            if (r.Resultado == ResultadoFacturaProveedor.Timbrado)
            {
                if (i.XmlEnviado is null || r.Uuid is null || r.Uuid == Guid.Empty || r.FechaTimbradoUtc is null ||
                    string.IsNullOrWhiteSpace(r.XmlTimbrado) || r.XmlTimbrado.Length > 120_000 ||
                    string.IsNullOrWhiteSpace(r.SelloSat) || string.IsNullOrWhiteSpace(r.NoCertificadoSat) || r.NoCertificadoSat.Length > 40)
                    return Task.FromResult<Resultado>(ErrorNegocio.Validacion("timbre-incompleto", "La respuesta de timbrado está incompleta."));
                i.Estado = f.Estado = "timbrado";
                i.Uuid = f.Uuid = r.Uuid; i.FechaTimbradoUtc = f.FechaTimbradoUtc = r.FechaTimbradoUtc;
                i.XmlTimbrado = r.XmlTimbrado; i.NoCertificadoSat = r.NoCertificadoSat;
                i.SelloSat = r.SelloSat; i.CadenaOriginalSat = r.CadenaOriginalSat;
            }
            else
            {
                // Solo se descarta sin consultar cuando el XML nunca llegó a quedar listo para enviarse.
                if (r.Resultado == ResultadoFacturaProveedor.ErrorDePreparacion && i.XmlEnviado is not null)
                    return Task.FromResult<Resultado>(Modificada());
                i.Estado = f.Estado = "error";
                i.CodigoError = r.CodigoError ?? "error-de-emision";
            }
            f.ActualizadaUtc = DateTime.UtcNow;
            Auditar(datos, f, i, "factura_compra_resuelta");
            return Task.FromResult(Resultado.Exito());
        }, ct);

    public async Task<IReadOnlyList<IntentoFacturaCompraDto>> ObtenerPendientesAsync(DateTime limiteUtc, int maximo, CancellationToken ct)
    {
        var limite = new[] { limiteUtc, DateTime.UtcNow.AddMinutes(-30) }.Min();
        var ids = await db.IntentosFacturasProveedor.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Estado == "timbrando" && x.ActualizadaUtc < limite)
            .OrderBy(x => x.ActualizadaUtc).Take(Math.Clamp(maximo, 1, 100))
            .Select(x => new { x.Id, x.CuentaId }).ToListAsync(ct);
        var salida = new List<IntentoFacturaCompraDto>();
        foreach (var candidato in ids)
        {
            await using var datos = Contexto(candidato.CuentaId);
            await using var tx = await datos.Database.BeginTransactionAsync(ct);
            await Bloquear(datos, $"factura-proveedor-intento:{candidato.Id:N}", ct);
            var i = await datos.IntentosFacturasProveedor.SingleAsync(x => x.Id == candidato.Id, ct);
            if (i.Estado != "timbrando" || i.ActualizadaUtc >= limite) continue;
            i.Control = Guid.NewGuid(); i.ActualizadaUtc = DateTime.UtcNow;
            var f = await datos.FacturasDeCompras.SingleAsync(x => x.Id == i.FacturaId, ct);
            Auditar(datos, f, i, "factura_compra_conciliacion");
            await datos.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            salida.Add(ADto(f, i, false));
        }
        return salida;
    }

    private async Task<Resultado> Cambiar(Guid intentoId, Guid control,
        Func<AppDbContext, FacturaDeCompra, IntentoFacturaProveedor, Task<Resultado>> cambio, CancellationToken ct)
    {
        var ubicacion = await Localizar(intentoId, ct);
        if (ubicacion is null) return NoEncontrada();
        await using var datos = Contexto(ubicacion.CuentaId);
        await using var tx = await datos.Database.BeginTransactionAsync(ct);
        await Bloquear(datos, $"factura-proveedor-intento:{intentoId:N}", ct);
        var i = await datos.IntentosFacturasProveedor.SingleAsync(x => x.Id == intentoId, ct);
        if (i.Control != control || i.Estado != "timbrando") return Modificada();
        var f = await datos.FacturasDeCompras.SingleAsync(x => x.Id == i.FacturaId, ct);
        var r = await cambio(datos, f, i);
        if (r.EsFallo) return r;
        i.ActualizadaUtc = DateTime.UtcNow;
        await datos.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return r;
    }

    private AppDbContext Contexto(Guid cuenta)
    {
        var contexto = new ContextoEmpresaFijo(cuenta: cuenta);
        return new(new DbContextOptionsBuilder<AppDbContext>().Configurar(db.Database.GetConnectionString(), contexto).Options, contexto);
    }
    private Task<IntentoFacturaProveedor?> Localizar(Guid id, CancellationToken ct)
        => db.IntentosFacturasProveedor.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    private void Auditar(AppDbContext datos, FacturaDeCompra f, IntentoFacturaProveedor i, string accion)
        => new ServicioDeBitacora(datos, new ContextoEmpresaFijo(cuenta: f.CuentaId), operador, accesor)
            .Registrar("FacturaDeCompra", f.Id.ToString(), accion,
                despues: new { i.Id, i.Estado, i.Folio, i.Uuid, i.CodigoError, i.CertificadoId },
                cuentaId: f.CuentaId, empresaId: f.EmpresaId, operadorId: i.OperadorId);
    private CsdDescifradoDto Descifrar(CertificadoProveedor c)
        => new(c.NumeroSerie,
            protecciones.CreateProtector("Facturacion.CsdProveedor.v1", c.Rfc, "cer").Unprotect(c.CerCifrado),
            protecciones.CreateProtector("Facturacion.CsdProveedor.v1", c.Rfc, "key").Unprotect(c.KeyCifrada),
            protecciones.CreateProtector("Facturacion.CsdProveedor.v1", c.Rfc, "contrasena").Unprotect(c.ContrasenaCifrada),
            c.VigenciaDesdeUtc, c.VigenciaHastaUtc);
    public static void Limpiar(CsdDescifradoDto c)
    { CryptographicOperations.ZeroMemory(c.CertificadoCer); CryptographicOperations.ZeroMemory(c.LlavePrivadaKey); }
    private static Task Bloquear(AppDbContext datos, string recurso, CancellationToken ct)
        => datos.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={recurso}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51000, 'No se pudo bloquear la factura del proveedor.', 1;", ct);
    private static ErrorNegocio NoEncontrada() => ErrorNegocio.NoEncontrado("factura-no-encontrada", "No se encontró la factura de compra.");
    private static ErrorNegocio Modificada() => ErrorNegocio.Conflicto("intento-modificado", "La factura cambió o ya se está procesando. Actualiza su estado.");
    private static IntentoFacturaCompraDto ADto(FacturaDeCompra f, IntentoFacturaProveedor i, bool nuevo)
        => new(i.Id, i.Control, f.CompraId, nuevo, i.Estado, $"proveedor-{i.Id:N}", i.Folio, i.FechaEmisionUtc,
            new(new(f.EmisorRfc, f.EmisorNombre, f.EmisorRegimen, f.EmisorCodigoPostal, f.EmisorZonaHoraria, f.Serie),
                new(f.ReceptorRfc, f.ReceptorNombre, f.ReceptorRegimen, f.ReceptorCodigoPostal, f.UsoCfdi),
                f.Descripcion, f.ClaveProdServ, f.ClaveUnidad, f.FormaPago, f.Subtotal, f.Iva, f.TasaIva, f.Total),
            i.XmlEnviado is null ? null : new(i.XmlEnviado, i.CadenaOriginal!, i.Sello!, i.NoCertificado!));
}
