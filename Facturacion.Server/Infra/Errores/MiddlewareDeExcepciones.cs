using System.Diagnostics;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Infra.Errores;

/// <summary>
/// Convierte toda excepción no controlada en una respuesta Problem Details con su traza.
/// <para>
/// El detalle interno —tipo de excepción, mensaje, pila— se registra en el log y
/// <b>nunca</b> viaja al cliente. Lo único que cruza es el <c>traceId</c>, que es lo que el
/// usuario le dicta a soporte para que soporte encuentre el resto (ARQUITECTURA.md §4).
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
        catch (ErrorDeNegocioExcepcion negocio)
        {
            // No es una falla: es un resultado previsto que llegó por excepción solo porque el
            // contrato congelado no tiene dónde devolverlo. Sale con el mismo Problem Details
            // que cualquier otro error de negocio y se registra como advertencia.
            registro.LogWarning(
                "Error de negocio {Codigo} en {Metodo} {Ruta}",
                negocio.Error.Codigo, contexto.Request.Method, contexto.Request.Path.Value);

            if (contexto.Response.HasStarted) throw;

            var problema = negocio.Error.AProblema(
                Activity.Current?.Id ?? contexto.TraceIdentifier, contexto.Request.Path.Value);

            contexto.Response.Clear();
            contexto.Response.StatusCode = problema.Estado;
            contexto.Response.ContentType = TipoContenido;

            await contexto.Response.WriteAsJsonAsync(problema, options: null, contentType: TipoContenido);
        }
        catch (Exception excepcion)
        {
            var traceId = Activity.Current?.Id ?? contexto.TraceIdentifier;

            // Una petición mal formada es culpa de quien la mandó, no una falla del
            // servidor: registrarla como error llenaría el log de ruido y devolver 500
            // mandaría al usuario a soporte por algo que no tiene nada que investigar.
            var peticionInvalida = excepcion is BadHttpRequestException;

            if (peticionInvalida)
                registro.LogWarning(excepcion,
                    "Petición mal formada en {Metodo} {Ruta} con traza {TraceId}",
                    contexto.Request.Method, contexto.Request.Path.Value, traceId);
            else
                registro.LogError(excepcion,
                    "Excepción no controlada en {Metodo} {Ruta} con traza {TraceId}",
                    contexto.Request.Method, contexto.Request.Path.Value, traceId);

            // Si ya se empezó a escribir la respuesta no se puede reemplazar: se corta la
            // conexión, que es preferible a entregar un cuerpo a medias.
            if (contexto.Response.HasStarted) throw;

            var estado = peticionInvalida
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status500InternalServerError;

            contexto.Response.Clear();
            contexto.Response.StatusCode = estado;
            contexto.Response.ContentType = TipoContenido;

            // Ni en el 400 se filtra el detalle interno: decir en qué byte falló el JSON no
            // le sirve a nadie legítimo y sí describe el modelo por dentro (ARQUITECTURA.md §4).
            var problema = peticionInvalida
                ? new DetalleProblema(
                    Tipo: "urn:facturacion:error:peticion-invalida",
                    Titulo: "La petición no se pudo leer",
                    Estado: estado,
                    Detalle: "El cuerpo de la petición no tiene el formato esperado.",
                    Instancia: contexto.Request.Path.Value,
                    TraceId: traceId,
                    Errores: null)
                : new DetalleProblema(
                    Tipo: "urn:facturacion:error:interno",
                    Titulo: "Ocurrió un error inesperado",
                    Estado: estado,
                    Detalle: "Vuelve a intentarlo. Si sigue ocurriendo, repórtalo a soporte con el identificador de traza.",
                    Instancia: contexto.Request.Path.Value,
                    TraceId: traceId,
                    Errores: null);

            await contexto.Response.WriteAsJsonAsync(problema, options: null, contentType: TipoContenido);
        }
    }
}
