using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Idempotencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;

namespace Facturacion.Server.Modules.Documentos.Cancelacion;

/// <summary>
/// Cancelación ante el SAT y consulta de estatus (B8, §30 del documento funcional).
///
/// <para><b>Por qué exige <c>Idempotency-Key</c> si no gasta timbre</b></para>
/// ARQUITECTURA.md §4 lo pide para todo <c>POST</c> que cobre o timbre. Cancelar no hace ninguna de
/// las dos, pero sí es irreversible ante el SAT y llega por el mismo camino que provoca envíos
/// repetidos: un doble clic, o el reintento del navegador cuando la respuesta tarda. Sin la
/// clave, el segundo envío entra como petición nueva y se topa con el comprobante ya en
/// <c>en_cancelacion</c>, devolviendo un conflicto que confunde a quien solo hizo clic dos veces.
///
/// <para><b>Van bajo <c>cancelar</c> y no bajo <c>timbrar</c></b></para>
/// Son permisos distintos en ARQUITECTURA.md §4 a propósito: quien captura y timbra no
/// necesariamente puede deshacer un documento fiscal ya emitido.
/// </summary>
public static class CancelacionEndpoints
{
    public static void MapCancelacion(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/documentos")
            .WithTags("Documentos")
            .RequireAuthorization(Permisos.Cancelar);

        grupo.MapPost("/{id:guid}/cancelar", Cancelar)
            .AddEndpointFilter<FiltroDeIdempotencia>();

        grupo.MapPost("/{id:guid}/estatus-sat", ConsultarEstatus);
    }

    private static async Task<IResult> Cancelar(
        Guid id, PeticionDeCancelacion peticion, ServicioDeCancelacion cancelacion,
        HttpContext http, CancellationToken ct)
    {
        var resultado = await cancelacion.CancelarAsync(id, peticion, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(http)
            : Results.Ok(resultado.Valor);
    }

    /// <summary>
    /// Es <c>POST</c> y no <c>GET</c> porque no solo lee: alinea el estatus local con lo que
    /// conteste el SAT, que es justo lo que saca de <c>en_cancelacion</c> a un comprobante cuya
    /// solicitud se quedó sin respuesta.
    /// </summary>
    private static async Task<IResult> ConsultarEstatus(
        Guid id, ServicioDeCancelacion cancelacion, HttpContext http, CancellationToken ct)
    {
        var resultado = await cancelacion.ConsultarEstatusAsync(id, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(http)
            : Results.Ok(resultado.Valor);
    }
}
