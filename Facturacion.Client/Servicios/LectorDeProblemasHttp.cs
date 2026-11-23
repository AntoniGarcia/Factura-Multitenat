using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Facturacion.Shared.Comun;

namespace Facturacion.Client.Servicios;

/// <summary>
/// Lee una respuesta HTTP fallida como <see cref="DetalleProblema"/> sin lanzar
/// <see cref="JsonException"/> cuando el cuerpo viene vacío (401 sintético tras fallo de
/// refresh) o en HTML (errores 413/502/503 de IIS o proxy).
/// </summary>
public static class LectorDeProblemasHttp
{
    public static async Task<DetalleProblema> LeerProblemaAsync(
        this HttpResponseMessage respuesta, CancellationToken ct = default)
    {
        try
        {
            if (respuesta.Content is not null)
            {
                var problema = await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
                if (problema is not null)
                    return problema;
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            // Cuerpo vacío o no JSON: se construye un DetalleProblema genérico según el código HTTP.
        }

        var estado = (int)respuesta.StatusCode;
        return respuesta.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new DetalleProblema(
                "urn:facturacion:error:sesion-caducada",
                "Sesión caducada",
                estado,
                "Tu sesión caducó. Vuelve a iniciar sesión.",
                null, string.Empty, null),
            HttpStatusCode.Forbidden => new DetalleProblema(
                "urn:facturacion:error:sin-permiso",
                "Sin permiso",
                estado,
                "No tienes permiso para realizar esta acción.",
                null, string.Empty, null),
            HttpStatusCode.RequestEntityTooLarge => new DetalleProblema(
                "urn:facturacion:error:archivo-demasiado-grande",
                "Archivo demasiado grande",
                estado,
                "El archivo supera el tamaño máximo permitido por el servidor.",
                null, string.Empty, null),
            _ => new DetalleProblema(
                "urn:facturacion:error:respuesta-inesperada",
                "No se pudo completar la operación",
                estado,
                "El servidor devolvió una respuesta inesperada. Vuelve a intentarlo en un momento.",
                null, string.Empty, null)
        };
    }
}
