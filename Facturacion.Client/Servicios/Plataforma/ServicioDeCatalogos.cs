using System.Net;
using System.Net.Http.Json;
using Facturacion.Client.Componentes.Comunes;
using Facturacion.Shared.Contratos;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>
/// Precarga los catálogos chicos del SAT al iniciar sesión y los cachea en memoria durante
/// la sesión, revalidando con ETag (CLAUDE.md §7). Es el que alimenta selects instantáneos
/// —régimen fiscal, uso de CFDI, forma de pago...— sin ida y vuelta al servidor; para
/// autocompletar contra catálogos grandes está <see cref="BuscadorCatalogo"/> con
/// <see cref="IBusquedaDeCatalogo"/>.
/// <para>
/// Solo memoria, nunca <c>localStorage</c>: un catálogo del SAT cacheado y viejo produce
/// comprobantes mal emitidos (CLAUDE.md §4), así que se pierde al cerrar la pestaña como el
/// resto del estado de sesión, y <see cref="Componentes.Comunes.PuertaDeArranque"/> lo vuelve
/// a pedir en cada arranque.
/// </para>
/// </summary>
public sealed class ServicioDeCatalogos(IHttpClientFactory fabrica)
{
    private readonly Dictionary<string, IReadOnlyList<ClaveSatDto>> _cache = new();
    private string? _etag;

    /// <summary>Trae los catálogos precargables. Se llama una vez, desde la puerta de arranque.</summary>
    public Task InicializarAsync(CancellationToken ct = default) => RevalidarAsync(ct);

    /// <summary>
    /// Vuelve a pedir los catálogos al servidor. Con ETag: si nada cambió desde la última
    /// vez, el servidor responde 304 y esta llamada no reemplaza la caché ni gasta ancho de
    /// banda en volver a mandar los mismos datos.
    /// </summary>
    public async Task RevalidarAsync(CancellationToken ct = default)
    {
        var cliente = fabrica.CreateClient(ClientesHttp.Api);
        using var peticion = new HttpRequestMessage(HttpMethod.Get, "api/catalogos/precargables");

        if (_etag is not null)
            peticion.Headers.TryAddWithoutValidation("If-None-Match", _etag);

        using var respuesta = await cliente.SendAsync(peticion, ct);

        if (respuesta.StatusCode == HttpStatusCode.NotModified)
            return;

        if (!respuesta.IsSuccessStatusCode)
            return; // Sin catálogos precargados el sistema sigue usable: BuscadorCatalogo consulta directo al servidor.

        var datos = await respuesta.Content.ReadFromJsonAsync<Dictionary<string, IReadOnlyList<ClaveSatDto>>>(ct);
        if (datos is null) return;

        _cache.Clear();
        foreach (var (catalogo, claves) in datos)
            _cache[catalogo] = claves;

        _etag = respuesta.Headers.ETag?.Tag;
    }

    /// <summary>Las claves vigentes de un catálogo precargado, o vacío si no está en la lista de CLAUDE.md §7.</summary>
    public IReadOnlyList<ClaveSatDto> Obtener(string catalogo)
        => _cache.GetValueOrDefault(catalogo, []);
}
