using System.Net;
using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.ComercioExterior;
using Facturacion.Shared.Comun;

namespace Facturacion.Client.Servicios.Documentos;

public sealed class ServicioDeComercioExterior(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    public async Task<(DatosComercioExteriorDto? Datos, DetalleProblema? Error)> ObtenerAsync(
        Guid comprobanteId, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.GetAsync($"api/comercio-exterior/comprobantes/{comprobanteId}", ct);
        if (respuesta.StatusCode == HttpStatusCode.NoContent) return (null, null);
        return await LeerAsync(respuesta, ct);
    }

    public async Task<(DatosComercioExteriorDto? Datos, DetalleProblema? Error)> GuardarAsync(
        Guid comprobanteId, DatosComercioExteriorDto datos, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync($"api/comercio-exterior/comprobantes/{comprobanteId}", datos, ct);
        return await LeerAsync(respuesta, ct);
    }

    public async Task<(ArchivoParaDescarga? Archivo, DetalleProblema? Error)> ObtenerXmlBorradorAsync(
        Guid comprobanteId, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.GetAsync($"api/comercio-exterior/comprobantes/{comprobanteId}/xml-borrador", ct);
        if (!respuesta.IsSuccessStatusCode)
            return (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
        var nombre = respuesta.Content.Headers.ContentDisposition?.FileNameStar ??
            respuesta.Content.Headers.ContentDisposition?.FileName?.Trim('"') ??
            $"borrador-complemento-comercio-exterior-{comprobanteId:N}.xml";
        return (new ArchivoParaDescarga(nombre, "application/xml",
            await respuesta.Content.ReadAsByteArrayAsync(ct)), null);
    }

    public async Task<(string? Mensaje, DetalleProblema? Error)> ValidarXmlAsync(
        Guid comprobanteId, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsync($"api/comercio-exterior/comprobantes/{comprobanteId}/validar-xml", null, ct);
        return respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<string>(ct), null)
            : (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
    }

    private static async Task<(DatosComercioExteriorDto? Datos, DetalleProblema? Error)> LeerAsync(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<DatosComercioExteriorDto>(ct), null)
            : (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
}
