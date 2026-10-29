using System.Net;
using System.Net.Http.Json;
using Facturacion.Client.Componentes.Comunes;
using Facturacion.Shared.Contratos;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>
/// Catálogos pequeños en memoria, revalidados con ETag mientras se captura.
/// </summary>
public sealed class ServicioDeCatalogos(IHttpClientFactory fabrica)
{
    private readonly Dictionary<string, IReadOnlyList<ClaveSatDto>> _cache = new();
    private string? _etag;
    private readonly SemaphoreSlim _exclusividad = new(1, 1);
    private DateTimeOffset _ultimaConsulta;
    public long Revision { get; private set; }

    public async Task ActualizarSiCorrespondeAsync(CancellationToken ct = default)
    {
        await _exclusividad.WaitAsync(ct);
        try
        {
            if (DateTimeOffset.UtcNow - _ultimaConsulta < TimeSpan.FromSeconds(4)) return;
            await RevalidarAsync(ct);
            _ultimaConsulta = DateTimeOffset.UtcNow;
        }
        finally { _exclusividad.Release(); }
    }

    /// <summary>Carga inicial de los catálogos pequeños al entrar.</summary>
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
        Revision++;
    }

    /// <summary>Claves vigentes del catálogo precargado; devuelve una lista vacía si no está disponible.</summary>
    public IReadOnlyList<ClaveSatDto> Obtener(string catalogo)
        => _cache.GetValueOrDefault(catalogo, []);

    /// <summary>
    /// Opciones de captura: el peso, que siempre está, y las monedas que la empresa agregó en
    /// Configuración. No modifica el catálogo SAT ni los documentos guardados.
    /// </summary>
    public IReadOnlyList<ClaveSatDto> ObtenerMonedasDeCaptura(IEnumerable<string> deLaEmpresa)
    {
        var claves = new HashSet<string>(deLaEmpresa) { "MXN" };

        return [.. Obtener("c_Moneda").Where(x => x.Vigente && claves.Contains(x.Clave))
            .OrderBy(x => x.Clave == "MXN" ? 0 : 1).ThenBy(x => x.Clave)];
    }
}
