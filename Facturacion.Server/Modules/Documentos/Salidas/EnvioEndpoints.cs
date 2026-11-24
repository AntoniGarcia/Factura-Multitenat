using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;

namespace Facturacion.Server.Modules.Documentos.Salidas;

// El servicio exige el permiso de enviar del tipo de documento.
public static class EnvioEndpoints
{
    public static void MapEnvios(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/documentos")
            .WithTags("Documentos")
            .RequireAuthorization(Permisos.Politicas.Enviar);

        grupo.MapGet("/{id:guid}/envio", ObtenerPropuesta);
        grupo.MapPost("/{id:guid}/enviar", Enviar);
    }

    private static async Task<IResult> ObtenerPropuesta(
        Guid id, ServicioDeEnvioDeComprobantes envios, HttpContext http, CancellationToken ct)
    {
        var resultado = await envios.ObtenerPropuestaAsync(id, ct);

        return resultado.EsFallo ? resultado.Error!.AResultado(http) : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> Enviar(
        Guid id, PeticionDeEnvio peticion, ServicioDeEnvioDeComprobantes envios, HttpContext http, CancellationToken ct)
    {
        var resultado = await envios.EnviarAsync(id, peticion, ct);

        return resultado.EsFallo ? resultado.Error!.AResultado(http) : Results.Ok(resultado.Valor);
    }
}
