using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.Salidas;

public static class DescargaEndpoints
{
    public static void MapDescargas(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/documentos")
            .WithTags("Documentos")
            .RequireAuthorization(Permisos.Timbrar);

        grupo.MapGet("/{id:guid}/xml", ObtenerXml);
        grupo.MapGet("/{id:guid}/pdf", ObtenerPdf);
        grupo.MapGet("/{id:guid}/descarga", ObtenerZip);
    }

    private static async Task<IResult> ObtenerXml(Guid id, ServicioDeDescargas descargas, HttpContext http, CancellationToken ct)
        => Archivo(await descargas.ObtenerXmlAsync(id, ct), http);

    private static async Task<IResult> ObtenerPdf(Guid id, ServicioDeDescargas descargas, HttpContext http, CancellationToken ct)
        => Archivo(await descargas.ObtenerPdfAsync(id, ct), http);

    private static async Task<IResult> ObtenerZip(Guid id, ServicioDeDescargas descargas, HttpContext http, CancellationToken ct)
        => Archivo(await descargas.ObtenerZipAsync(id, ct), http);

    private static IResult Archivo(Resultado<ArchivoDescarga> resultado, HttpContext http)
    {
        if (resultado.EsFallo) return resultado.Error!.AResultado(http);

        var archivo = resultado.Valor;
        return Results.File(archivo.Contenido, archivo.TipoMime, archivo.NombreArchivo);
    }
}