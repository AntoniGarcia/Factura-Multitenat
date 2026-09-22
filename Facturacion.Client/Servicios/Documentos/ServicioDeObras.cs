using System.Net.Http.Json;
using System.Net;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Obras;

namespace Facturacion.Client.Servicios.Documentos;

public sealed class ServicioDeObras(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    public async Task<(DatosObraDto? Exito, DetalleProblema? Error)> ObtenerAsync(
        Guid comprobanteId, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.GetAsync($"api/obras/comprobantes/{comprobanteId}", ct);
        if (respuesta.StatusCode == HttpStatusCode.NoContent) return (null, null);
        return await LeerAsync(respuesta, ct);
    }

    public async Task<(DatosObraDto? Exito, DetalleProblema? Error)> GuardarAsync(
        Guid comprobanteId, PeticionGuardarDatosObra peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync($"api/obras/comprobantes/{comprobanteId}", peticion, ct);
        return await LeerAsync(respuesta, ct);
    }

    public async Task<(ArchivoParaDescarga? Archivo, DetalleProblema? Error)> ObtenerEstimacionPdfAsync(
        Guid comprobanteId, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.GetAsync($"api/obras/comprobantes/{comprobanteId}/estimacion.pdf", ct);
        if (!respuesta.IsSuccessStatusCode)
            return (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));

        var contenido = await respuesta.Content.ReadAsByteArrayAsync(ct);
        var nombre = respuesta.Content.Headers.ContentDisposition?.FileNameStar
            ?? respuesta.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? $"estimacion-obra-{comprobanteId:N}.pdf";
        return (new ArchivoParaDescarga(nombre, "application/pdf", contenido), null);
    }

    private static async Task<(DatosObraDto? Exito, DetalleProblema? Error)> LeerAsync(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<DatosObraDto>(ct), null)
            : (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
}
