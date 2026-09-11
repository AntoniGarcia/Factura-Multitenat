using System.Net.Http.Json;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;
using Facturacion.Client.Servicios.Plataforma;

namespace Facturacion.Client.Servicios.Documentos;

/// <summary>Llama a <c>/api/documentos</c>. La empresa nunca viaja: va en el token.</summary>
public sealed class ServicioDeEmision(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    public async Task<ComprobanteDto> CrearBorradorAsync(CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsync("api/documentos/borradores", null, ct);
        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<ComprobanteDto>(ct))!;
    }

    public Task<ComprobanteDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
        => Cliente.GetFromJsonAsync<ComprobanteDto>($"api/documentos/{id}", ct);

    public async Task<EstadoDeIntegracionFiscalDto> ObtenerEstadoDeIntegracionAsync(
        CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<EstadoDeIntegracionFiscalDto>(
            "api/documentos/integracion-fiscal", ct)
           ?? new EstadoDeIntegracionFiscalDto(false);

    public async Task<(ComprobanteDto? Exito, DetalleProblema? Error)> GuardarAsync(
        Guid id, PeticionGuardarBorrador peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync($"api/documentos/{id}", peticion, ct);
        return await LeerAsync<ComprobanteDto>(respuesta, ct);
    }

    public async Task<DetalleProblema?> EliminarBorradorAsync(Guid id, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.DeleteAsync($"api/documentos/{id}", ct);

        return respuesta.IsSuccessStatusCode
            ? null
            : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }

    public async Task<(CfdiRelacionadoResueltoDto? Exito, DetalleProblema? Error)> ResolverRelacionadoAsync(
        string serie, int folio, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.GetAsync(
            $"api/documentos/relacionados/resolver?serie={Uri.EscapeDataString(serie)}&folio={folio}", ct);

        return await LeerAsync<CfdiRelacionadoResueltoDto>(respuesta, ct);
    }

    /// <summary>Timbra el borrador. Requiere una clave nueva por cada intento real del usuario.</summary>
    public async Task<(RespuestaDeTimbradoDto? Exito, DetalleProblema? Error)> TimbrarAsync(
        Guid id, string claveIdempotencia, CancellationToken ct = default)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Post, $"api/documentos/{id}/timbrar");
        peticion.Headers.Add("Idempotency-Key", claveIdempotencia);

        using var respuesta = await Cliente.SendAsync(peticion, ct);
        return await LeerAsync<RespuestaDeTimbradoDto>(respuesta, ct);
    }

    private static async Task<(T? Exito, DetalleProblema? Error)> LeerAsync<T>(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<T>(ct), null)
            : (default, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
}
