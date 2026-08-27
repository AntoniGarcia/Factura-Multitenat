using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Operador;

namespace Facturacion.Client.Servicios.Operador;

/// <summary>Llama a <c>/api/operador/cuentas</c>: las cuentas contratantes y su ficha.</summary>
public sealed class ServicioDeClientesDePlataforma(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.ApiOperador);

    public async Task<PaginaDeCuentas> ListarAsync(
        string? texto, int pagina, int tamano, CancellationToken ct = default)
    {
        var ruta = $"api/operador/cuentas?pagina={pagina}&tamano={tamano}";

        if (!string.IsNullOrWhiteSpace(texto)) ruta += $"&texto={Uri.EscapeDataString(texto)}";

        return await Cliente.GetFromJsonAsync<PaginaDeCuentas>(ruta, ct)
               ?? new PaginaDeCuentas([], 0);
    }

    public async Task<DetalleDeCuentaDto?> ObtenerAsync(Guid cuentaId, CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<DetalleDeCuentaDto>($"api/operador/cuentas/{cuentaId}", ct);
}
