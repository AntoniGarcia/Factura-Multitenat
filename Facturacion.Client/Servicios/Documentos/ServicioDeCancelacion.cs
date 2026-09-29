using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;

namespace Facturacion.Client.Servicios.Documentos;

/// <summary>Cancelación ante el SAT y consulta de estatus (B8).</summary>
public sealed class ServicioDeCancelacion(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    /// <summary>
    /// Cancela el comprobante. Pide una clave nueva por cada intento real del usuario: es lo
    /// que impide que un doble clic se convierta en dos solicitudes ante el SAT.
    /// </summary>
    public async Task<(ResultadoDeCancelacionDto? Exito, DetalleProblema? Error)> CancelarAsync(
        Guid id, PeticionDeCancelacion peticion, string claveIdempotencia, CancellationToken ct = default)
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Post, $"api/documentos/{id}/cancelar")
        {
            Content = JsonContent.Create(peticion)
        };

        mensaje.Headers.Add("Idempotency-Key", claveIdempotencia);

        using var respuesta = await Cliente.SendAsync(mensaje, ct);
        return await LeerAsync<ResultadoDeCancelacionDto>(respuesta, ct);
    }

    public async Task<(EstatusSatDto? Exito, DetalleProblema? Error)> ConsultarEstatusAsync(
        Guid id, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsync($"api/documentos/{id}/estatus-sat", null, ct);
        return await LeerAsync<EstatusSatDto>(respuesta, ct);
    }

    public async Task<(PaginaDeSolicitudesDeCancelacion? Exito, DetalleProblema? Error)> ListarSolicitudesAsync(
        string filtro, string? busca, int pagina, int tamano, CancellationToken ct = default)
    {
        var ruta = $"api/cancelaciones?filtro={Uri.EscapeDataString(filtro)}&pagina={pagina}&tamano={tamano}";

        if (!string.IsNullOrWhiteSpace(busca))
            ruta += $"&busca={Uri.EscapeDataString(busca.Trim())}";

        using var respuesta = await Cliente.GetAsync(ruta, ct);
        return await LeerAsync<PaginaDeSolicitudesDeCancelacion>(respuesta, ct);
    }

    public async Task<(ResultadoDeVerificacionMasivaDto? Exito, DetalleProblema? Error)> VerificarAbiertasAsync(
        CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsync("api/cancelaciones/verificar", null, ct);
        return await LeerAsync<ResultadoDeVerificacionMasivaDto>(respuesta, ct);
    }

    private static async Task<(T? Exito, DetalleProblema? Error)> LeerAsync<T>(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<T>(ct), null)
            : (default, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
}
