using Microsoft.JSInterop;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Facturacion.Shared.Comun;

namespace Facturacion.Client.Servicios.Plataforma;

public sealed record ArchivoParaDescarga(string Nombre, string TipoContenido, byte[] Contenido);

/// <summary>
/// Entrega al navegador un archivo que ya descargó un cliente HTTP autenticado. No abre la
/// ruta directamente porque esa navegación perdería el access token que vive en memoria.
/// </summary>
public sealed class ServicioDeDescargas(IJSRuntime js, IHttpClientFactory fabrica)
{
    private Task<IJSObjectReference>? _modulo;

    public async Task<DetalleProblema?> DescargarAsync(string ruta, string nombrePredeterminado, CancellationToken ct = default)
    {
        try
        {
            using var respuesta = await fabrica.CreateClient(ClientesHttp.Api).GetAsync(ruta, ct);
            if (!respuesta.IsSuccessStatusCode)
            {
                if (respuesta.Content.Headers.ContentType?.MediaType is "application/problem+json" or "application/json")
                {
                    try
                    {
                        var problema = await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
                        if (problema is not null) return problema;
                    }
                    catch (JsonException) { }
                }
                return ErrorDeDescarga((int)respuesta.StatusCode, respuesta.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "Tu sesión venció. Vuelve a iniciar sesión.",
                    HttpStatusCode.Forbidden => "No tienes permiso para descargar este archivo.",
                    _ => "No se pudo descargar el archivo. Vuelve a intentarlo."
                });
            }

            var nombre = respuesta.Content.Headers.ContentDisposition?.FileNameStar
                ?? respuesta.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? nombrePredeterminado;
            var tipo = respuesta.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            await GuardarAsync(new(nombre, tipo, await respuesta.Content.ReadAsByteArrayAsync(ct)));
            return null;
        }
        catch (HttpRequestException)
        {
            return ErrorDeDescarga(503, "No se pudo conectar con el servidor. Vuelve a intentarlo.");
        }
        catch (JSException)
        {
            return ErrorDeDescarga(500, "No se pudo guardar el archivo. Recarga la página y vuelve a intentarlo.");
        }
    }

    private static DetalleProblema ErrorDeDescarga(int estado, string mensaje)
        => new("descarga-fallida", "No se pudo descargar", estado, mensaje, null, "", null);

    public async ValueTask GuardarAsync(ArchivoParaDescarga archivo)
    {
        using var flujo = new MemoryStream(archivo.Contenido, writable: false);
        using var referencia = new DotNetStreamReference(flujo);

        var modulo = await ModuloAsync();
        await modulo.InvokeVoidAsync(
            "guardarDescarga", archivo.Nombre, archivo.TipoContenido, referencia);
    }

    public async ValueTask<string> CrearVistaPreviaAsync(ArchivoParaDescarga archivo)
    {
        using var flujo = new MemoryStream(archivo.Contenido, writable: false);
        using var referencia = new DotNetStreamReference(flujo);

        var modulo = await ModuloAsync();
        return await modulo.InvokeAsync<string>(
            "crearVistaPrevia", archivo.TipoContenido, referencia);
    }

    public async ValueTask LiberarVistaPreviaAsync(string url)
    {
        var modulo = await ModuloAsync();
        await modulo.InvokeVoidAsync("liberarVistaPrevia", url);
    }

    private Task<IJSObjectReference> ModuloAsync()
        => _modulo ??= js.InvokeAsync<IJSObjectReference>("import", "./descargas.js").AsTask();
}
