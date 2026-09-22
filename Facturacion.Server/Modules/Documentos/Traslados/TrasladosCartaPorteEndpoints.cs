using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Transporte;

namespace Facturacion.Server.Modules.Documentos.Traslados;

/// <summary>Endpoints de borradores para traslado de mercancía propia por autotransporte.</summary>
public static class TrasladosCartaPorteEndpoints
{
    public static void MapTrasladosCartaPorte(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/traslados-carta-porte")
            .WithTags("Carta Porte")
            .RequireAuthorization(Permisos.Timbrar);

        grupo.MapPost("", Crear);
        grupo.MapGet("/{comprobanteId:guid}", Obtener);
        grupo.MapPut("/{comprobanteId:guid}", Actualizar);
        grupo.MapPost("/{comprobanteId:guid}/validar-xml", ValidarXml);
        grupo.MapGet("/{comprobanteId:guid}/vista-previa.pdf", VistaPreviaPdf);
        grupo.MapGet("/{comprobanteId:guid}/xml-borrador", DescargarXmlBorrador);
    }

    private static async Task<IResult> Crear(
        PeticionGuardarTrasladoCartaPorte peticion, ServicioDeTrasladosCartaPorte servicio,
        HttpContext http, CancellationToken ct)
    {
        var resultado = await servicio.CrearAsync(peticion, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(http) : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> Obtener(
        Guid comprobanteId, ServicioDeTrasladosCartaPorte servicio, HttpContext http, CancellationToken ct)
    {
        var traslado = await servicio.ObtenerAsync(comprobanteId, ct);
        return traslado is null
            ? ErrorNegocio.NoEncontrado("traslado-no-encontrado", "No se encontró ese traslado Carta Porte.").AResultado(http)
            : Results.Ok(traslado);
    }

    private static async Task<IResult> Actualizar(
        Guid comprobanteId, PeticionGuardarTrasladoCartaPorte peticion, ServicioDeTrasladosCartaPorte servicio,
        HttpContext http, CancellationToken ct)
    {
        var resultado = await servicio.ActualizarAsync(comprobanteId, peticion, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(http) : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> ValidarXml(
        Guid comprobanteId, ServicioDeTrasladosCartaPorte servicio, HttpContext http, CancellationToken ct)
    {
        var resultado = await servicio.ValidarXmlAsync(comprobanteId, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(http) : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> VistaPreviaPdf(Guid comprobanteId, ServicioDeSalidasCartaPorte salidas, HttpContext http, CancellationToken ct)
    {
        var resultado = await salidas.PdfAsync(comprobanteId, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(http) : Results.File(resultado.Valor!.Contenido, resultado.Valor.TipoContenido, resultado.Valor.Nombre);
    }

    private static async Task<IResult> DescargarXmlBorrador(Guid comprobanteId, ServicioDeSalidasCartaPorte salidas, HttpContext http, CancellationToken ct)
    {
        var resultado = await salidas.XmlAsync(comprobanteId, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(http) : Results.File(resultado.Valor!.Contenido, resultado.Valor.TipoContenido, resultado.Valor.Nombre);
    }
}
