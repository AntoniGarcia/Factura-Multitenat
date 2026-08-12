using System.Text.Json.Serialization;

namespace Facturacion.Shared.Comun;

/// <summary>
/// Cuerpo de toda respuesta de error del servidor, según RFC 7807.
/// No se usa <c>Microsoft.AspNetCore.Mvc.ProblemDetails</c> porque obligaría a esta
/// biblioteca —que también viaja al navegador dentro del WebAssembly— a referenciar
/// el framework de ASP.NET Core.
/// El <see cref="TraceId"/> se muestra en pantalla: es lo que el usuario le dicta a soporte.
/// </summary>
public sealed record DetalleProblema(
    [property: JsonPropertyName("type")] string Tipo,
    [property: JsonPropertyName("title")] string Titulo,
    [property: JsonPropertyName("status")] int Estado,
    [property: JsonPropertyName("detail")] string? Detalle,
    [property: JsonPropertyName("instance")] string? Instancia,
    [property: JsonPropertyName("traceId")] string TraceId,
    [property: JsonPropertyName("errores")] IReadOnlyDictionary<string, string[]>? Errores);
