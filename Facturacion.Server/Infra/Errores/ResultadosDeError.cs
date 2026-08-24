using System.Diagnostics;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Infra.Errores;

/// <summary>
/// Convierte un error de negocio en la respuesta HTTP. Toda respuesta de error del sistema
/// sale por aquí, en Problem Details y con su traza (ARQUITECTURA.md §4).
/// </summary>
public static class ResultadosDeError
{
    private const string TipoContenido = "application/problem+json";

    public static IResult AResultado(this ErrorNegocio error, HttpContext contexto)
    {
        var problema = error.AProblema(TrazaDe(contexto), contexto.Request.Path.Value);

        return Results.Json(problema, statusCode: problema.Estado, contentType: TipoContenido);
    }

    /// <summary>Problem Details para lo que no pasa por un <see cref="ErrorNegocio"/>: 401 y 403.</summary>
    public static async Task EscribirProblema(
        HttpContext contexto, int estado, string tipo, string titulo, string? detalle)
    {
        if (contexto.Response.HasStarted) return;

        contexto.Response.Clear();
        contexto.Response.StatusCode = estado;
        contexto.Response.ContentType = TipoContenido;

        var problema = new DetalleProblema(
            Tipo: $"urn:facturacion:error:{tipo}",
            Titulo: titulo,
            Estado: estado,
            Detalle: detalle,
            Instancia: contexto.Request.Path.Value,
            TraceId: TrazaDe(contexto),
            Errores: null);

        await contexto.Response.WriteAsJsonAsync(problema, options: null, contentType: TipoContenido);
    }

    private static string TrazaDe(HttpContext contexto)
        => Activity.Current?.Id ?? contexto.TraceIdentifier;
}
