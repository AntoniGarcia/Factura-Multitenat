using Facturacion.Server.Data;
using Facturacion.Server.Infra.Almacen;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Empresas;

/// <summary>
/// Logo de la empresa para el PDF. Se guarda cifrado y fuera de <c>wwwroot</c>, y solo sale
/// por un endpoint autorizado (ARQUITECTURA.md §4).
/// </summary>
public sealed class ServicioDeLogo(
    AppDbContext baseDeDatos,
    IAlmacenDeArchivos almacen,
    IContextoEmpresaInterno contexto,
    IServicioDeBitacora bitacora)
{
    /// <summary>Dos megabytes. Un logo para un PDF no necesita más, y el límite acota el abuso.</summary>
    private const int TamanoMaximo = 2 * 1024 * 1024;

    public async Task<Resultado<string>> GuardarAsync(
        string nombreOriginal, byte[] contenido, CancellationToken ct)
    {
        if (contenido.Length == 0)
            return ErrorNegocio.Validacion("logo-vacio", "El archivo llegó vacío.");

        if (contenido.Length > TamanoMaximo)
            return ErrorNegocio.Validacion("logo-muy-grande",
                $"El logo pesa {contenido.Length / 1024d / 1024d:0.#} MB y el máximo son 2 MB.");

        // Se mira el contenido real, no la extensión: renombrar un .exe a .png es trivial, y
        // lo que acabaría en el PDF —o peor, en el disco del servidor— sería otra cosa.
        var tipo = TipoPorContenido(contenido);

        if (tipo is null)
            return ErrorNegocio.Validacion("logo-formato-no-admitido",
                "El archivo no es PNG ni JPG. Cambiarle la extensión no cambia lo que es.");

        var empresa = await baseDeDatos.Empresas.FirstOrDefaultAsync(e => e.Id == contexto.EmpresaId, ct);

        if (empresa is null)
            return ErrorNegocio.NoEncontrado("empresa-no-encontrada", "No se encontró la empresa activa.");

        var ruta = await almacen.GuardarAsync(empresa.Id, CategoriasDeArchivo.Logo, contenido, ct);

        empresa.LogoRuta = ruta;
        empresa.LogoTipoMime = tipo;
        empresa.LogoNombreOriginal = nombreOriginal;

        bitacora.Registrar(
            EntidadesDeBitacora.Empresa, empresa.Id.ToString(), AccionesDeBitacora.LogoActualizado,
            despues: new { nombreOriginal, tipo, bytes = contenido.Length });

        await baseDeDatos.SaveChangesAsync(ct);

        return tipo;
    }

    public async Task<ArchivoDto?> ObtenerAsync(CancellationToken ct)
    {
        var empresa = await baseDeDatos.Empresas
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == contexto.EmpresaId, ct);

        if (empresa?.LogoRuta is null) return null;

        var contenido = await almacen.LeerAsync(empresa.Id, CategoriasDeArchivo.Logo, empresa.LogoRuta, ct);

        return new ArchivoDto(contenido, empresa.LogoTipoMime ?? "application/octet-stream",
            empresa.LogoNombreOriginal ?? "logo");
    }

    /// <summary>
    /// Quita la referencia al logo. <b>No borra el archivo</b>: en este sistema nada se borra
    /// (ARQUITECTURA.md §5), y un PDF viejo podría seguir necesitándolo.
    /// </summary>
    public async Task<bool> QuitarAsync(CancellationToken ct)
    {
        var empresa = await baseDeDatos.Empresas.FirstOrDefaultAsync(e => e.Id == contexto.EmpresaId, ct);

        if (empresa?.LogoRuta is null) return false;

        bitacora.Registrar(
            EntidadesDeBitacora.Empresa, empresa.Id.ToString(), AccionesDeBitacora.LogoQuitado,
            antes: new { empresa.LogoNombreOriginal });

        empresa.LogoRuta = null;
        empresa.LogoTipoMime = null;
        empresa.LogoNombreOriginal = null;

        await baseDeDatos.SaveChangesAsync(ct);

        return true;
    }

    /// <summary>Firma del archivo: PNG y JPG traen bytes fijos al principio.</summary>
    private static string? TipoPorContenido(byte[] contenido) => contenido switch
    {
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => "image/png",
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        _ => null
    };
}
