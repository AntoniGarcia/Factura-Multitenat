using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Idempotencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;

namespace Facturacion.Server.Modules.Documentos.Cancelacion;

/// <summary>Cancelación y consulta de estatus ante el SAT. Requiere permiso de cancelación y una clave de idempotencia para evitar solicitudes duplicadas.</summary>
public static class CancelacionEndpoints
{
    public static void MapCancelacion(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/documentos")
            .WithTags("Documentos")
            .RequireAuthorization(Permisos.Politicas.Cancelar);

        grupo.MapPost("/{id:guid}/cancelar", Cancelar)
            .AddEndpointFilter<FiltroDeIdempotencia>();

        grupo.MapPost("/{id:guid}/estatus-sat", ConsultarEstatus);

        var solicitudes = rutas.MapGroup("/api/cancelaciones")
            .WithTags("Documentos")
            .RequireAuthorization(Permisos.Politicas.Cancelar);

        solicitudes.MapGet("", ListarSolicitudes);
        solicitudes.MapPost("/verificar", VerificarAbiertas);
    }

    private static async Task<IResult> ListarSolicitudes(
        ServicioDeSolicitudesDeCancelacion solicitudes, HttpContext http, CancellationToken ct,
        string? filtro = null, string? busca = null, int pagina = 0, int tamano = 25)
    {
        var resultado = await solicitudes.ListarAsync(
            filtro, busca, Math.Max(pagina, 0), Math.Clamp(tamano, 1, 100), ct);

        return resultado.EsFallo ? resultado.Error!.AResultado(http) : Results.Ok(resultado.Valor);
    }

    /// <summary>
    /// Sin <c>Idempotency-Key</c>: repetirla no cambia nada que la primera no haya cambiado, solo
    /// vuelve a preguntar al SAT.
    /// </summary>
    private static async Task<IResult> VerificarAbiertas(
        ServicioDeSolicitudesDeCancelacion solicitudes, HttpContext http, CancellationToken ct)
    {
        var resultado = await solicitudes.VerificarAbiertasAsync(ct);

        return resultado.EsFallo ? resultado.Error!.AResultado(http) : Results.Ok(resultado.Valor);
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
