using System.Diagnostics;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Infra.Errores;

/// <summary>
/// Convierte toda excepción no controlada en una respuesta Problem Details con su traza.
/// <para>
/// El detalle interno —tipo de excepción, mensaje, pila— se registra en el log y
/// <b>nunca</b> viaja al cliente. Lo único que cruza es el <c>traceId</c>, que es lo que el
/// usuario le dicta a soporte para que soporte encuentre el resto (CLAUDE.md §4).
/// </para>
/// </summary>
public sealed class MiddlewareDeExcepciones(RequestDelegate siguiente, ILogger<MiddlewareDeExcepciones> registro)
{
    private const string TipoContenido = "application/problem+json";

    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await siguiente(contexto);
        }
        catch (Exception excepcion)
        {
            var traceId = Activity.Current?.Id ?? contexto.TraceIdentifier;

            registro.LogError(excepcion,
                "Excepción no controlada en {Metodo} {Ruta} con traza {TraceId}",
                contexto.Request.Method, contexto.Request.Path.Value, traceId);

            // Si ya se empezó a escribir la respuesta no se puede reemplazar: se corta la
            // conexión, que es preferible a entregar un cuerpo a medias.
            if (contexto.Response.HasStarted) throw;

            contexto.Response.Clear();
            contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
            contexto.Response.ContentType = TipoContenido;

            var problema = new DetalleProblema(
                Tipo: "urn:facturacion:error:interno",
                Titulo: "Ocurrió un error inesperado",
                Estado: StatusCodes.Status500InternalServerError,
                Detalle: "Vuelve a intentarlo. Si sigue ocurriendo, repórtalo a soporte con el identificador de traza.",
                Instancia: contexto.Request.Path.Value,
                TraceId: traceId,
                Errores: null);

            await contexto.Response.WriteAsJsonAsync(problema, options: null, contentType: TipoContenido);
        }
    }
}
