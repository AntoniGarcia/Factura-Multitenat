using System.Net.Http.Json;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>
/// Llama a <c>/api/timbres</c>. La empresa nunca viaja: va en el token.
///
/// <para><b>El precio no se manda, se muestra</b></para>
/// <see cref="ComprarAsync"/> envía únicamente el identificador del paquete. Los precios que
/// esta clase trae son para pintarlos en pantalla; el que se cobra lo vuelve a leer el
/// servidor de su propio catálogo. Este código corre en el navegador y es legible y
/// modificable, así que nada de lo que salga de aquí puede decidir cuánto se cobra
/// (ARQUITECTURA.md §3 y §4).
/// </summary>
public sealed class ServicioDeTimbres(IHttpClientFactory fabrica) : IIndicadorDeTimbres
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    public async Task<int> ObtenerDisponiblesAsync()
    {
        var saldo = await SaldoAsync();
        return saldo.Disponibles;
    }

    public async Task<SaldoTimbresDto> SaldoAsync(CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<SaldoTimbresDto>("api/timbres/saldo", ct)
           ?? new SaldoTimbresDto(0, 0);

    public async Task<IReadOnlyList<PaqueteDto>> PaquetesAsync(CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<IReadOnlyList<PaqueteDto>>("api/timbres/paquetes", ct) ?? [];

    public async Task<IReadOnlyList<CompraDto>> ComprasAsync(CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<IReadOnlyList<CompraDto>>("api/timbres/compras", ct) ?? [];

    /// <summary>
    /// La membresía puede no existir todavía, y el servidor responde 204 en ese caso. Sin este
    /// trato explícito, <c>GetFromJsonAsync</c> revienta al intentar leer un cuerpo vacío.
    /// </summary>
    public async Task<MembresiaDto?> MembresiaAsync(CancellationToken ct = default)
    {
        using var respuesta = await Cliente.GetAsync("api/timbres/membresia", ct);

        if (respuesta.StatusCode == System.Net.HttpStatusCode.NoContent) return null;

        return respuesta.IsSuccessStatusCode
            ? await respuesta.Content.ReadFromJsonAsync<MembresiaDto>(ct)
            : null;
    }

    public async Task<IReadOnlyList<MovimientoTimbreDto>> MovimientosAsync(
        string? tipo, DateTime? desde, DateTime? hasta, CancellationToken ct = default)
    {
        var url = "api/timbres/movimientos" + Filtros(tipo, desde, hasta);

        return await Cliente.GetFromJsonAsync<IReadOnlyList<MovimientoTimbreDto>>(url, ct) ?? [];
    }

    /// <param name="claveIdempotencia">
    /// La genera la pantalla una sola vez por intención de compra y la reutiliza en cada
    /// reintento. Es lo que convierte un doble clic, o un reintento del navegador tras una
    /// red intermitente, en una sola compra (ARQUITECTURA.md §4).
    /// </param>
    public async Task<(CompraDto? Exito, DetalleProblema? Error)> ComprarAsync(
        Guid paqueteId, string claveIdempotencia, CancellationToken ct = default)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Post, "api/timbres/comprar")
        {
            Content = JsonContent.Create(new PeticionDeCompra(paqueteId))
        };

        peticion.Headers.Add("Idempotency-Key", claveIdempotencia);

        using var respuesta = await Cliente.SendAsync(peticion, ct);

        return respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<CompraDto>(ct), null)
            : (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
    }

    public static string RutaDeExportacion(string? tipo, DateTime? desde, DateTime? hasta)
        => "api/timbres/movimientos/exportar" + Filtros(tipo, desde, hasta);

    private static string Filtros(string? tipo, DateTime? desde, DateTime? hasta)
    {
        var partes = new List<string>();

        if (!string.IsNullOrWhiteSpace(tipo)) partes.Add($"tipo={Uri.EscapeDataString(tipo)}");
        if (desde is not null) partes.Add($"desde={desde:O}");
        if (hasta is not null) partes.Add($"hasta={hasta:O}");

        return partes.Count == 0 ? string.Empty : "?" + string.Join('&', partes);
    }
}
