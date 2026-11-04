using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
using Facturacion.Shared.Contratos;

namespace Facturacion.Client.Servicios.Operador;

/// <summary>Llama a <c>/api/operador/compras</c>: consultar, acreditar y descartar.</summary>
public sealed class ServicioDeComprasDeOperador(
    IHttpClientFactory fabrica,
    ServicioDeDescargas descargas)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.ApiOperador);

    public async Task<DetalleProblema?> TimbrarFacturaAsync(Guid compraId, byte[] version, string clave)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Post, $"api/operador/compras/{compraId}/factura/timbrar")
        { Content = JsonContent.Create(new PeticionTimbrarFacturaCompra(version)) };
        peticion.Headers.Add("Idempotency-Key", clave);
        using var r = await Cliente.SendAsync(peticion);
        if (r.IsSuccessStatusCode) return null;
        if (r.Content.Headers.ContentType?.MediaType is "application/json" or "application/problem+json")
            return await r.Content.ReadFromJsonAsync<DetalleProblema>();
        return new("timbrado-compra", "No se pudo solicitar el timbrado", (int)r.StatusCode,
            "Actualiza la factura y vuelve a intentarlo.", null, "", null);
    }

    public async Task<(FacturaDeCompraDto? Datos, DetalleProblema? Error)> ObtenerFacturaAsync(Guid compraId)
    {
        using var r = await Cliente.GetAsync($"api/operador/compras/{compraId}/factura");
        return await LeerFacturaAsync(r);
    }

    public async Task<(FacturaDeCompraDto? Datos, DetalleProblema? Error)> GuardarFacturaAsync(Guid compraId, PeticionGuardarFacturaCompra p)
    {
        using var r = await Cliente.PutAsJsonAsync($"api/operador/compras/{compraId}/factura", p);
        return await LeerFacturaAsync(r);
    }

    public async Task<IReadOnlyList<ClaveSatDto>> CatalogoFacturaAsync(
        string catalogo, string? texto = null, string? clave = null, CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<List<ClaveSatDto>>(
            $"api/operador/compras/facturas/catalogos/{Uri.EscapeDataString(catalogo)}?texto={Uri.EscapeDataString(texto ?? "")}&clave={Uri.EscapeDataString(clave ?? "")}", ct) ?? [];

    private static async Task<(FacturaDeCompraDto? Datos, DetalleProblema? Error)> LeerFacturaAsync(HttpResponseMessage r)
    {
        if (r.IsSuccessStatusCode) return (await r.Content.ReadFromJsonAsync<FacturaDeCompraDto>(), null);
        if (r.Content.Headers.ContentType?.MediaType is "application/json" or "application/problem+json")
            return (null, await r.Content.ReadFromJsonAsync<DetalleProblema>());
        return (null, new DetalleProblema("factura-compra", "No se pudo abrir la factura", (int)r.StatusCode,
            "Revisa la conexión con el servidor.", null, "", null));
    }

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

    public async Task<DetalleProblema?> DescargarComprobanteAsync(
        Guid compraId, CancellationToken ct = default)
    {
        var (archivo, error) = await ObtenerComprobanteAsync(compraId, ct);

        if (archivo is not null)
            await descargas.GuardarAsync(archivo);

        return error;
    }

    public async Task<(ArchivoParaDescarga? Archivo, DetalleProblema? Error)> ObtenerComprobanteAsync(
        Guid compraId, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.GetAsync(
            $"api/operador/compras/{compraId}/comprobante", ct);

        if (!respuesta.IsSuccessStatusCode)
            return (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));

        var contenido = await respuesta.Content.ReadAsByteArrayAsync(ct);
        return (new ArchivoParaDescarga(
            $"comprobante-compra-{compraId:N}.pdf", "application/pdf", contenido), null);
    }
}
