using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Notaria;

namespace Facturacion.Server.Modules.Documentos.Notaria;

/// <summary>Configuración de Notaría, aislada de la empresa por su licencia comercial.</summary>
public static class NotariaEndpoints
{
    public static void MapNotaria(this IEndpointRouteBuilder rutas)
    {
        var grupoConfiguracion = rutas.MapGroup("/api/notaria")
            .WithTags("Notaría")
            .RequireAuthorization(Permisos.ConfigurarEmpresa);

        grupoConfiguracion.MapGet("/configuracion", Obtener);
        grupoConfiguracion.MapPut("/configuracion", Guardar);

        var grupoDocumentos = rutas.MapGroup("/api/notaria/comprobantes")
            .WithTags("Notaría")
            .RequireAuthorization(Permisos.Timbrar);
        grupoDocumentos.MapGet("/{comprobanteId:guid}", ObtenerDatos);
        grupoDocumentos.MapPut("/{comprobanteId:guid}", GuardarDatos);
        grupoDocumentos.MapGet("/{comprobanteId:guid}/partes", ObtenerPartes);
        grupoDocumentos.MapPut("/{comprobanteId:guid}/partes", GuardarPartes);
        grupoDocumentos.MapPost("/{comprobanteId:guid}/validar-xml", ValidarXml);
    }

    private static async Task<IResult> Obtener(
        ServicioDeNotaria notaria, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await notaria.ObtenerAsync(ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) :
            resultado.Valor is null ? Results.NoContent() : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> Guardar(
        PeticionGuardarConfiguracionNotario peticion,
        ServicioDeNotaria notaria,
        HttpContext contexto,
        CancellationToken ct)
    {
        var resultado = await notaria.GuardarAsync(peticion, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> ObtenerDatos(
        Guid comprobanteId, ServicioDeNotaria notaria, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await notaria.ObtenerParaComprobanteAsync(comprobanteId, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) :
            resultado.Valor is null ? Results.NoContent() : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> GuardarDatos(
        Guid comprobanteId,
        PeticionGuardarDatosNotaria peticion,
        ServicioDeNotaria notaria,
        HttpContext contexto,
        CancellationToken ct)
    {
        var resultado = await notaria.GuardarParaComprobanteAsync(comprobanteId, peticion, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> ObtenerPartes(
        Guid comprobanteId, ServicioDeNotaria notaria, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await notaria.ObtenerPartesAsync(comprobanteId, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) :
            resultado.Valor is null ? Results.NoContent() : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> GuardarPartes(
        Guid comprobanteId,
        PeticionGuardarPartesNotariales peticion,
        ServicioDeNotaria notaria,
        HttpContext contexto,
        CancellationToken ct)
    {
        var resultado = await notaria.GuardarPartesAsync(comprobanteId, peticion, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> ValidarXml(
        Guid comprobanteId, ServicioDeNotaria notaria, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await notaria.ValidarXmlAsync(comprobanteId, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.Ok(resultado.Valor);
    }
}
