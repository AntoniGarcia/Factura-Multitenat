using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Client.Servicios.Operador;

public sealed class ServicioDeCatalogosDeOperador(IHttpClientFactory fabrica)
{
    public async Task<(ActualizacionCatalogosDto? Datos, DetalleProblema? Error)> ObtenerActualizacionAsync(CancellationToken ct = default)
    {
        using var respuesta = await fabrica.CreateClient(ClientesHttp.ApiOperador).GetAsync("api/operador/catalogos-sat/actualizacion", ct);
        if (!respuesta.IsSuccessStatusCode) return (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
        if (respuesta.StatusCode == System.Net.HttpStatusCode.NoContent) return (null, null);
        return (await respuesta.Content.ReadFromJsonAsync<ActualizacionCatalogosDto>(ct), null);
    }

    public async Task<(ActualizacionCatalogosDto? Datos, DetalleProblema? Error)> ActualizarAsync(
        string catalogo, Stream archivo, string nombre, CancellationToken ct = default)
    {
        using var contenido = new MultipartFormDataContent { { new StreamContent(archivo), "archivo", nombre } };
        using var respuesta = await fabrica.CreateClient(ClientesHttp.ApiOperador).PostAsync(
            $"api/operador/catalogos-sat/{Uri.EscapeDataString(catalogo)}/actualizar", contenido, ct);
        if (respuesta.IsSuccessStatusCode) return (await respuesta.Content.ReadFromJsonAsync<ActualizacionCatalogosDto>(ct), null);
        if (respuesta.Content.Headers.ContentType?.MediaType == "application/problem+json" || respuesta.Content.Headers.ContentType?.MediaType == "application/json")
            return (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
        return (null, new DetalleProblema("archivo-no-recibido", "No se pudo cargar el archivo", (int)respuesta.StatusCode,
            "Revisa el tamaño del archivo y el límite de carga del servidor.", null, "", null));
    }

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
