using System.Net.Http.Json;
using Facturacion.Shared.Contratos;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>Fuente de datos de <see cref="Componentes.Comunes.BuscadorCatalogo"/> y <see cref="Componentes.Comunes.ModalCatalogo"/>.</summary>
public interface IBusquedaDeCatalogo
{
    Task<IReadOnlyList<ClaveSatDto>> BuscarAsync(string catalogo, string texto, CancellationToken ct);
}

/// <summary>Llama a <c>GET /api/catalogos/{catalogo}/buscar</c> (ARQUITECTURA.md §7).</summary>
public sealed class BusquedaDeCatalogo(IHttpClientFactory fabrica) : IBusquedaDeCatalogo
{
    public async Task<IReadOnlyList<ClaveSatDto>> BuscarAsync(string catalogo, string texto, CancellationToken ct)
    {
        var cliente = fabrica.CreateClient(ClientesHttp.Api);

        var url = $"api/catalogos/{Uri.EscapeDataString(catalogo)}/buscar?texto={Uri.EscapeDataString(texto)}&tope=20";
        using var respuesta = await cliente.GetAsync(url, ct);

        if (!respuesta.IsSuccessStatusCode)
            return [];

        var resultado = await respuesta.Content.ReadFromJsonAsync<IReadOnlyList<ClaveSatDto>>(ct);
        return resultado ?? [];
    }
}
