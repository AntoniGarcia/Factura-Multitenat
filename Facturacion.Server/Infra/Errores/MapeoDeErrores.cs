using Facturacion.Shared.Comun;

namespace Facturacion.Server.Infra.Errores;

/// <summary>
/// Traducción de un error de negocio a Problem Details. Es la contraparte de lo que
/// <see cref="TipoErrorNegocio"/> promete en su documentación: si la tabla viviera solo en
/// un comentario, se desincronizaría en la primera fase que agregue un tipo.
/// </summary>
public static class MapeoDeErrores
{
    public static int ACodigoHttp(this TipoErrorNegocio tipo) => tipo switch
    {
        TipoErrorNegocio.Validacion => StatusCodes.Status400BadRequest,
        TipoErrorNegocio.NoEncontrado => StatusCodes.Status404NotFound,
        TipoErrorNegocio.Conflicto => StatusCodes.Status409Conflict,
        TipoErrorNegocio.SinPermiso => StatusCodes.Status403Forbidden,
        TipoErrorNegocio.ReglaDeNegocio => StatusCodes.Status422UnprocessableEntity,
        TipoErrorNegocio.LimiteExcedido => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status500InternalServerError
    };

    public static DetalleProblema AProblema(this ErrorNegocio error, string traceId, string? instancia)
        => new(
            Tipo: $"urn:facturacion:error:{error.Codigo}",
            Titulo: error.Mensaje,
            Estado: error.Tipo.ACodigoHttp(),
            Detalle: null,
            Instancia: instancia,
            TraceId: traceId,
            Errores: error.Errores);
}
