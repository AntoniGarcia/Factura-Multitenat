using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Client.Servicios.Operador;

/// <summary>Llama a <c>/api/operador/compras</c>: consultar, acreditar y descartar.</summary>
public sealed class ServicioDeComprasDeOperador(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.ApiOperador);

    public async Task<PaginaDeComprasDeOperador> ListarAsync(
        string? estado, string? texto, int pagina, int tamano, CancellationToken ct = default)
    {
        var ruta = $"api/operador/compras?pagina={pagina}&tamano={tamano}";

        if (!string.IsNullOrWhiteSpace(estado)) ruta += $"&estado={Uri.EscapeDataString(estado)}";
        if (!string.IsNullOrWhiteSpace(texto)) ruta += $"&texto={Uri.EscapeDataString(texto)}";

        return await Cliente.GetFromJsonAsync<PaginaDeComprasDeOperador>(ruta, ct)
               ?? new PaginaDeComprasDeOperador([], 0);
    }

    /// <summary>
    /// Acredita el pago.
    /// <para>
    /// No manda clave de idempotencia: el endpoint no la lleva, porque quien impide la doble
    /// acreditación es el procedimiento almacenado, que solo actúa si la compra sigue
    /// pendiente. Ver el comentario de <c>ComprasDeOperadorEndpoints</c>.
    /// </para>
    /// </summary>
    public async Task<DetalleProblema?> AcreditarAsync(Guid compraId, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsync(
            $"api/operador/compras/{compraId}/acreditar", content: null, ct);

        return respuesta.IsSuccessStatusCode
            ? null
            : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }

    public async Task<DetalleProblema?> RechazarAsync(
        Guid compraId, string motivo, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync(
            $"api/operador/compras/{compraId}/rechazar", new PeticionRechazarCompra(motivo), ct);

        return respuesta.IsSuccessStatusCode
            ? null
            : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }
}
