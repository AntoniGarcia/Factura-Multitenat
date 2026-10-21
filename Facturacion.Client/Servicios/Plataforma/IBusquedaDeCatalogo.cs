using System.Net.Http.Json;
using Facturacion.Shared.Contratos;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>Fuente de datos de <see cref="Componentes.Comunes.BuscadorCatalogo"/> y <see cref="Componentes.Comunes.ModalCatalogo"/>.</summary>
public interface IBusquedaDeCatalogo
{
    Task<ClaveSatDto?> ResolverAsync(string catalogo, string clave, CancellationToken ct);
    Task<IReadOnlyList<ClaveSatDto>> BuscarAsync(string catalogo, string texto, CancellationToken ct);
    Task<IReadOnlyList<ClaveSatDto>> OpcionesAsync(string catalogo, CancellationToken ct);
}

/// <summary>Llama a <c>GET /api/catalogos/{catalogo}/buscar</c>.</summary>
public sealed class BusquedaDeCatalogo(IHttpClientFactory fabrica) : IBusquedaDeCatalogo
{
    private readonly Dictionary<string, IReadOnlyList<ClaveSatDto>> _opciones = new(StringComparer.Ordinal);

    public async Task<ClaveSatDto?> ResolverAsync(string catalogo, string clave, CancellationToken ct)
    {
        var cliente = fabrica.CreateClient(ClientesHttp.Api);
        var url = $"api/catalogos/{Uri.EscapeDataString(catalogo)}/{Uri.EscapeDataString(clave)}";
        using var respuesta = await cliente.GetAsync(url, ct);
        if (respuesta.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<ClaveSatDto>(ct);
    }

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

    public async Task<IReadOnlyList<ClaveSatDto>> OpcionesAsync(string catalogo, CancellationToken ct)
    {
        if (_opciones.TryGetValue(catalogo, out var cache)) return cache;

        var cliente = fabrica.CreateClient(ClientesHttp.Api);
        using var respuesta = await cliente.GetAsync($"api/catalogos/{Uri.EscapeDataString(catalogo)}/opciones", ct);
        if (!respuesta.IsSuccessStatusCode) return [];

        var resultado = await respuesta.Content.ReadFromJsonAsync<IReadOnlyList<ClaveSatDto>>(ct) ?? [];
        _opciones[catalogo] = resultado;
        return resultado;
    }
}
