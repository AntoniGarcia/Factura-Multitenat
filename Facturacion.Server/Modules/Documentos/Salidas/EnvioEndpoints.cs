using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>
/// Envío de comprobantes por correo. Con <c>timbrar</c>, igual que la descarga: quien puede
/// descargar el XML ya puede mandárselo a quien quiera, así que un permiso aparte no protegería
/// nada, y la lista de permisos está cerrada (ARQUITECTURA.md §4).
/// </summary>
public static class EnvioEndpoints
{
    public static void MapEnvios(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/documentos")
            .WithTags("Documentos")
            .RequireAuthorization(Permisos.EnviarCorreo);

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
