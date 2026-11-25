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

    public async Task<DetalleProblema?> EliminarBorradorAsync(Guid id, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.DeleteAsync($"api/pagos/{id}", ct);

        return respuesta.IsSuccessStatusCode
            ? null
            : await respuesta.LeerProblemaAsync(ct);
    }

    public async Task<PagoDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.GetAsync($"api/pagos/{id}", ct);
        if (respuesta.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<PagoDto>(ct);
    }

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

    // Vacío si la consulta falla.
    public async Task<IReadOnlyList<FacturaPendienteDto>> BuscarPendientesAsync(
        Guid? clienteId, string? texto, CancellationToken ct = default)
    {
        var ruta = $"api/pagos/pendientes?texto={Uri.EscapeDataString(texto ?? "")}" +
            (clienteId is { } id ? $"&clienteId={id}" : "");
        using var respuesta = await Cliente.GetAsync(ruta, ct);
        if (!respuesta.IsSuccessStatusCode) return [];
        return await respuesta.Content.ReadFromJsonAsync<IReadOnlyList<FacturaPendienteDto>>(ct) ?? [];
    }

    private static async Task<(T? Exito, DetalleProblema? Error)> LeerAsync<T>(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<T>(ct), null)
            : (default, await respuesta.LeerProblemaAsync(ct));

    public async Task<(PagoDto? Exito, DetalleProblema? Error)> ObtenerParaSustituirAsync(Guid uuid, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.GetAsync($"api/pagos/sustitucion/{uuid}", ct);
        return await LeerAsync<PagoDto>(respuesta, ct);
    }
}
