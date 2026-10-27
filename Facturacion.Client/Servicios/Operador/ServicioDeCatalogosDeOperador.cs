using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Client.Servicios.Operador;

public sealed class ServicioDeCatalogosDeOperador(IHttpClientFactory fabrica)
{
    public async Task<(IReadOnlyList<CatalogoSatDeOperadorDto>? Datos, DetalleProblema? Error)> ListarAsync(CancellationToken ct = default)
    {
        using var respuesta = await fabrica.CreateClient(ClientesHttp.ApiOperador).GetAsync("api/operador/catalogos-sat", ct);
        return respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<IReadOnlyList<CatalogoSatDeOperadorDto>>(ct), null)
            : (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
    }

    public async Task<(PaginaCatalogoSatDto? Datos, DetalleProblema? Error)> ConsultarAsync(
        string catalogo, string texto, bool? vigentes, int pagina, int tamano, CancellationToken ct)
    {
        var ruta = $"api/operador/catalogos-sat/{Uri.EscapeDataString(catalogo)}?texto={Uri.EscapeDataString(texto)}&pagina={pagina}&tamano={tamano}" +
            (vigentes is { } vigente ? $"&vigentes={vigente.ToString().ToLowerInvariant()}" : "");
        using var respuesta = await fabrica.CreateClient(ClientesHttp.ApiOperador).GetAsync(ruta, ct);
        return respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<PaginaCatalogoSatDto>(ct), null)
            : (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
    }
}
