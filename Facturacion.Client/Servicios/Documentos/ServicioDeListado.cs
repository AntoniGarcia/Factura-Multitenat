using System.Globalization;
using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;

namespace Facturacion.Client.Servicios.Documentos;

/// <summary>Consulta el listado de documentos. La empresa nunca viaja: va en el token.</summary>
public sealed class ServicioDeListado(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    public async Task<(PaginaDeComprobantes? Exito, DetalleProblema? Error)> ListarAsync(
        string? texto, EstatusComprobante? estatus, DateTime? desdeUtc, DateTime? hastaUtc,
        int pagina, int tamano, string? orden, bool descendente, CancellationToken ct = default)
    {
        var parametros = new List<string>
        {
            $"pagina={pagina}",
            $"tamano={tamano}",
            $"descendente={descendente}"
        };

        if (!string.IsNullOrWhiteSpace(texto))
            parametros.Add($"texto={Uri.EscapeDataString(texto)}");

        if (estatus is { } valor)
            parametros.Add($"estatus={valor.ACadena()}");

        if (desdeUtc is { } desde)
            parametros.Add($"desdeUtc={Uri.EscapeDataString(desde.ToString("O", CultureInfo.InvariantCulture))}");

        if (hastaUtc is { } hasta)
            parametros.Add($"hastaUtc={Uri.EscapeDataString(hasta.ToString("O", CultureInfo.InvariantCulture))}");

        if (!string.IsNullOrWhiteSpace(orden))
            parametros.Add($"orden={Uri.EscapeDataString(orden)}");

        using var respuesta = await Cliente.GetAsync($"api/documentos?{string.Join("&", parametros)}", ct);

        return respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<PaginaDeComprobantes>(ct), null)
            : (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
    }
}
