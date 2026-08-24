using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Almacen;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Empresas;

/// <summary>
/// Carga y consulta de certificados de sello digital. Es la parte más delicada de la mitad A:
/// aquí entra al sistema el material con el que se firman comprobantes fiscales.
/// <para>
/// Nada de lo que entra aquí vuelve a salir hacia el <c>Client</c>: la pantalla solo ve
/// número de serie y vigencia. El material descifrado únicamente lo entrega
/// <see cref="ProveedorCsdParaTimbrado"/>, y con bitácora.
/// </para>
/// </summary>
public sealed class ServicioDeCsd(
    AppDbContext baseDeDatos,
    IAlmacenDeArchivos almacen,
    IProtectorDeSecretos secretos,
    IContextoEmpresaInterno contexto,
    IServicioDeBitacora bitacora)
{
    public async Task<IReadOnlyList<CertificadoCsdDto>> ListarAsync(CancellationToken ct)
    {
        var diasAviso = await DiasDeAvisoAsync(ct);

        var certificados = await baseDeDatos.CertificadosCsd
            .AsNoTracking()
            .OrderByDescending(c => c.Activo)
            .ThenByDescending(c => c.FechaCargaUtc)
            .ToListAsync(ct);

        return [.. certificados.Select(c => ADto(c, diasAviso))];
    }

    public async Task<Resultado<CertificadoCsdDto>> CargarAsync(
        byte[] cer, byte[] key, string contrasena, CancellationToken ct)
    {
        if (cer.Length == 0 || key.Length == 0)
            return ErrorNegocio.Validacion("archivos-incompletos", "Hacen falta el certificado y la llave privada.");

        if (string.IsNullOrEmpty(contrasena))
            return ErrorNegocio.Validacion("contrasena-vacia", "Escribe la contraseña de la llave privada.");

        var empresa = await baseDeDatos.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == contexto.EmpresaId, ct);

        if (empresa is null)
            return ErrorNegocio.NoEncontrado("empresa-no-encontrada", "No se encontró la empresa activa.");

        var leido = LectorDeCsd.Leer(cer, key, contrasena, empresa.Rfc);
        if (leido.EsFallo) return leido.Error!;

        var csd = leido.Valor;

        var yaExiste = await baseDeDatos.CertificadosCsd
            .AnyAsync(c => c.NumeroSerie == csd.NumeroSerie, ct);

        if (yaExiste)
            return ErrorNegocio.Conflicto("csd-duplicado",
                $"El certificado con número de serie {csd.NumeroSerie} ya está cargado en esta empresa.");

        // Solo se escribe al almacén después de que todas las validaciones pasaron: así no
        // quedan archivos huérfanos de intentos fallidos.
        var rutaCer = await almacen.GuardarAsync(empresa.Id, CategoriasDeArchivo.CertificadoCer, cer, ct);
        var rutaKey = await almacen.GuardarAsync(empresa.Id, CategoriasDeArchivo.LlavePrivadaKey, key, ct);

        var anterior = await baseDeDatos.CertificadosCsd.FirstOrDefaultAsync(c => c.Activo, ct);

        if (anterior is not null)
        {
            // Se conserva inactivo: los comprobantes timbrados con él llevan su número de
            // serie dentro y tiene que poder explicarse de dónde salió (ARQUITECTURA.md §5).
            anterior.Activo = false;

            bitacora.Registrar(
                EntidadesDeBitacora.CertificadoCsd, anterior.Id.ToString(), AccionesDeBitacora.CsdReemplazado,
                antes: new { anterior.NumeroSerie }, despues: new { csd.NumeroSerie });
        }

        var nuevo = new CertificadoCsd
        {
            Id = Guid.NewGuid(),
            NumeroSerie = csd.NumeroSerie,
            VigenciaDesdeUtc = csd.VigenciaDesdeUtc,
            VigenciaHastaUtc = csd.VigenciaHastaUtc,
            RutaCer = rutaCer,
            RutaKey = rutaKey,
            ContrasenaCifrada = secretos.Cifrar(empresa.Id, contrasena),
            Activo = true,
            FechaCargaUtc = DateTime.UtcNow,
            CargadoPorUsuarioId = contexto.UsuarioId
        };

        baseDeDatos.CertificadosCsd.Add(nuevo);

        // La bitácora registra el número de serie y la vigencia, nunca la contraseña ni el
        // contenido de los archivos.
        bitacora.Registrar(
            EntidadesDeBitacora.CertificadoCsd, nuevo.Id.ToString(), AccionesDeBitacora.CsdCargado,
            despues: new { nuevo.NumeroSerie, nuevo.VigenciaDesdeUtc, nuevo.VigenciaHastaUtc });

        await baseDeDatos.SaveChangesAsync(ct);

        return ADto(nuevo, await DiasDeAvisoAsync(ct));
    }

    private async Task<int> DiasDeAvisoAsync(CancellationToken ct)
        => await baseDeDatos.ConfiguracionesEmpresa
            .AsNoTracking()
            .Select(c => (int?)c.DiasAvisoCaducidadCertificado)
            .FirstOrDefaultAsync(ct) ?? 30;

    private static CertificadoCsdDto ADto(CertificadoCsd c, int diasAviso)
    {
        var dias = (int)Math.Floor((c.VigenciaHastaUtc - DateTime.UtcNow).TotalDays);

        return new CertificadoCsdDto(
            c.Id, c.NumeroSerie, c.VigenciaDesdeUtc, c.VigenciaHastaUtc, c.Activo, c.FechaCargaUtc,
            dias, c.Activo && dias <= diasAviso);
    }
}
