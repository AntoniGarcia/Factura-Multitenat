using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.ComercioExterior;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.ComercioExterior;

public static class ComercioExteriorEndpoints
{
    public static void MapComercioExterior(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/comercio-exterior/comprobantes")
            .WithTags("Comercio exterior").RequireAuthorization(Permisos.Timbrar);
        grupo.MapGet("/{comprobanteId:guid}", Obtener);
        grupo.MapPut("/{comprobanteId:guid}", Guardar);
        grupo.MapGet("/{comprobanteId:guid}/xml-borrador", XmlBorrador);
        grupo.MapPost("/{comprobanteId:guid}/validar-xml", ValidarXml);
    }

    private static async Task<IResult> Obtener(Guid comprobanteId, ServicioDeComercioExterior servicio,
        HttpContext contexto, CancellationToken ct)
    {
        var resultado = await servicio.ObtenerAsync(comprobanteId, ct);
        if (resultado.EsFallo) return resultado.Error!.AResultado(contexto);
        return resultado.Valor is null ? Results.NoContent() : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> Guardar(Guid comprobanteId, DatosComercioExteriorDto peticion,
        ServicioDeComercioExterior servicio, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await servicio.GuardarAsync(comprobanteId, peticion, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> XmlBorrador(Guid comprobanteId, ServicioDeComercioExterior servicio,
        HttpContext contexto, CancellationToken ct)
    {
        var resultado = await servicio.ObtenerXmlBorradorAsync(comprobanteId, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) :
            Results.File(resultado.Valor!.Contenido, resultado.Valor.TipoContenido, resultado.Valor.Nombre);
    }

    private static async Task<IResult> ValidarXml(Guid comprobanteId, ServicioDeComercioExterior servicio,
        HttpContext contexto, CancellationToken ct)
    {
        var resultado = await servicio.ValidarXmlCfdiAsync(comprobanteId, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.Ok(resultado.Valor);
    }
}
