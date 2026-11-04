using System.Net.Http.Json;
using System.Text.Json;
using Facturacion.Shared.Comun;
using Microsoft.JSInterop;

namespace Facturacion.Client.Servicios.Plataforma;

public sealed class ServicioDeArchivosDeFacturaCompra(IHttpClientFactory fabrica, ServicioDeDescargas descargas)
{
    public async Task<(ArchivoParaDescarga? Archivo, DetalleProblema? Error)> ObtenerAsync(
        Guid compraId, bool xml = false, bool operador = false, CancellationToken ct = default)
    {
        var extension = xml ? "xml" : "pdf";
        var ruta = operador ? "api/operador/compras" : "api/timbres/compras";
        try
        {
            using var r = await fabrica.CreateClient(operador ? ClientesHttp.ApiOperador : ClientesHttp.Api)
                .GetAsync($"{ruta}/{compraId}/factura/{extension}", ct);
            if (!r.IsSuccessStatusCode)
            {
                if (r.Content.Headers.ContentType?.MediaType is "application/problem+json" or "application/json")
                {
                    try
                    {
                        if (await r.Content.ReadFromJsonAsync<DetalleProblema>(ct) is { } error) return (null, error);
                    }
                    catch (JsonException) { }
                }
                return (null, Error((int)r.StatusCode, "No se pudo obtener la factura. Actualiza la página y vuelve a intentarlo."));
            }
            var tipo = xml ? "application/xml" : "application/pdf";
            if (r.Content.Headers.ContentType?.MediaType != tipo)
                return (null, Error(500, "El servidor no devolvió el archivo de la factura."));
            var nombre = r.Content.Headers.ContentDisposition?.FileNameStar
                ?? r.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? $"factura-compra-{compraId:N}.{extension}";
            return (new ArchivoParaDescarga(nombre, tipo, await r.Content.ReadAsByteArrayAsync(ct)), null);
        }
        catch (HttpRequestException) { return (null, Error(503, "No se pudo conectar con el servidor.")); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return (null, Error(504, "La descarga tardó demasiado. Vuelve a intentarlo.")); }
    }

    public async Task<DetalleProblema?> DescargarAsync(Guid compraId, bool xml = false, bool operador = false)
    {
        var (archivo, error) = await ObtenerAsync(compraId, xml, operador);
        if (archivo is null) return error;
        try { await descargas.GuardarAsync(archivo); return null; }
        catch (JSException) { return Error(500, "No se pudo guardar el archivo. Recarga la página e inténtalo de nuevo."); }
    }

    private static DetalleProblema Error(int estado, string mensaje)
        => new("archivo-factura-compra", "No se pudo descargar la factura", estado, mensaje, null, "", null);
}
