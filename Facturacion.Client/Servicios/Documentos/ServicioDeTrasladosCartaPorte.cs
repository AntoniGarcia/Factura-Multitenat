using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Transporte;

namespace Facturacion.Client.Servicios.Documentos;

/// <summary>Llama a los borradores de Carta Porte; la empresa activa siempre viaja en el token.</summary>
public sealed class ServicioDeTrasladosCartaPorte(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    public Task<TrasladoCartaPorteDto?> ObtenerAsync(Guid comprobanteId, CancellationToken ct = default)
        => Cliente.GetFromJsonAsync<TrasladoCartaPorteDto>($"api/traslados-carta-porte/{comprobanteId}", ct);

    public async Task<(TrasladoCartaPorteDto? Exito, DetalleProblema? Error)> CrearAsync(
        PeticionGuardarTrasladoCartaPorte peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync("api/traslados-carta-porte", peticion, ct);
        return await LeerAsync(respuesta, ct);
    }

    public async Task<(TrasladoCartaPorteDto? Exito, DetalleProblema? Error)> ActualizarAsync(
        Guid comprobanteId, PeticionGuardarTrasladoCartaPorte peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync($"api/traslados-carta-porte/{comprobanteId}", peticion, ct);
        return await LeerAsync(respuesta, ct);
    }

    public async Task<(ValidacionXmlCartaPorteDto? Exito, DetalleProblema? Error)> ValidarXmlAsync(
        Guid comprobanteId, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsync($"api/traslados-carta-porte/{comprobanteId}/validar-xml", null, ct);
        return respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<ValidacionXmlCartaPorteDto>(ct), null)
            : (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
    }

    public Task<(ArchivoParaDescarga? Archivo, DetalleProblema? Error)> ObtenerVistaPreviaPdfAsync(Guid comprobanteId, CancellationToken ct = default)
        => ObtenerArchivoAsync($"api/traslados-carta-porte/{comprobanteId}/vista-previa.pdf", $"borrador-carta-porte-{comprobanteId:N}.pdf", ct);

    public Task<(ArchivoParaDescarga? Archivo, DetalleProblema? Error)> ObtenerXmlBorradorAsync(Guid comprobanteId, CancellationToken ct = default)
        => ObtenerArchivoAsync($"api/traslados-carta-porte/{comprobanteId}/xml-borrador", $"borrador-carta-porte-{comprobanteId:N}.xml", ct);

    private static async Task<(TrasladoCartaPorteDto? Exito, DetalleProblema? Error)> LeerAsync(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<TrasladoCartaPorteDto>(ct), null)
            : (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));

    private async Task<(ArchivoParaDescarga? Archivo, DetalleProblema? Error)> ObtenerArchivoAsync(string ruta, string nombrePredeterminado, CancellationToken ct)
    {
        using var respuesta = await Cliente.GetAsync(ruta, ct);
        if (!respuesta.IsSuccessStatusCode) return (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
        var nombre = respuesta.Content.Headers.ContentDisposition?.FileNameStar ?? respuesta.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? nombrePredeterminado;
        return (new ArchivoParaDescarga(nombre, respuesta.Content.Headers.ContentType?.MediaType ?? "application/octet-stream", await respuesta.Content.ReadAsByteArrayAsync(ct)), null);
    }
}
