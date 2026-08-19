using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;

namespace Facturacion.Client.Servicios.Documentos;

/// <summary>Llama a <c>/api/pagos</c>. El timbrado va por <c>/api/documentos</c>, como el resto.</summary>
public sealed class ServicioDePagos(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    public async Task<PagoDto> CrearBorradorAsync(CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsync("api/pagos/borradores", null, ct);
        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<PagoDto>(ct))!;
    }

    public Task<PagoDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
        => Cliente.GetFromJsonAsync<PagoDto>($"api/pagos/{id}", ct);

    public async Task<(PagoDto? Exito, DetalleProblema? Error)> GuardarAsync(
        Guid id, PeticionGuardarPago peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync($"api/pagos/{id}", peticion, ct);
        return await LeerAsync<PagoDto>(respuesta, ct);
    }

    /// <summary>El botón «Consulta» de §27: saldo pendiente y parcialidad de una factura.</summary>
    public async Task<(DocumentoPorPagarDto? Exito, DetalleProblema? Error)> ConsultarSaldoAsync(
        string serie, int folio, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.GetAsync(
            $"api/pagos/por-pagar?serie={Uri.EscapeDataString(serie)}&folio={folio}", ct);

        return await LeerAsync<DocumentoPorPagarDto>(respuesta, ct);
    }

    private static async Task<(T? Exito, DetalleProblema? Error)> LeerAsync<T>(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<T>(ct), null)
            : (default, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
}
