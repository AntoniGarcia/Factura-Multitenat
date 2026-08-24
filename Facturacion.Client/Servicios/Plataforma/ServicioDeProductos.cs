using System.Net.Http.Json;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>Llama a <c>/api/productos</c>. La empresa nunca viaja: va en el token.</summary>
public sealed class ServicioDeProductos(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    public async Task<PaginaDeProductos> ListarAsync(
        string? texto, bool? activos, int pagina, int tamano,
        string? orden, bool descendente, CancellationToken ct = default)
    {
        // El filtro de estatus se omite de la URL cuando es "todos": un &activos= vacío
        // lo ata el enlazador del servidor a false, que es justo lo contrario.
        var url = $"api/productos?pagina={pagina}&tamano={tamano}" +
                  (activos is { } a ? $"&activos={(a ? "true" : "false")}" : "") +
                  $"&descendente={descendente}" +
                  (string.IsNullOrWhiteSpace(texto) ? "" : $"&texto={Uri.EscapeDataString(texto)}") +
                  (string.IsNullOrWhiteSpace(orden) ? "" : $"&orden={Uri.EscapeDataString(orden)}");

        return await Cliente.GetFromJsonAsync<PaginaDeProductos>(url, ct) ?? new PaginaDeProductos([], 0);
    }

    public Task<ProductoDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
        => Cliente.GetFromJsonAsync<ProductoDto>($"api/productos/{id}", ct);

    public async Task<(ProductoDto? Exito, DetalleProblema? Error)> CrearAsync(
        PeticionGuardarProducto peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync("api/productos", peticion, ct);
        return await LeerAsync<ProductoDto>(respuesta, ct);
    }

    public async Task<(ProductoDto? Exito, DetalleProblema? Error)> ActualizarAsync(
        Guid id, PeticionGuardarProducto peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync($"api/productos/{id}", peticion, ct);
        return await LeerAsync<ProductoDto>(respuesta, ct);
    }

    public async Task<(ProductoDto? Exito, DetalleProblema? Error)> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync($"api/productos/{id}/activo", new { activo }, ct);
        return await LeerAsync<ProductoDto>(respuesta, ct);
    }

    /// <summary>Sube el CSV y devuelve la vista previa. No guarda nada todavía.</summary>
    public async Task<(VistaPreviaDeImportacion? Exito, DetalleProblema? Error)> AnalizarCsvAsync(
        Stream archivo, string nombre, CancellationToken ct = default)
    {
        using var cuerpo = new MultipartFormDataContent { { new StreamContent(archivo), "archivo", nombre } };

        using var respuesta = await Cliente.PostAsync("api/productos/importar/analizar", cuerpo, ct);
        return await LeerAsync<VistaPreviaDeImportacion>(respuesta, ct);
    }

    public async Task<(ResultadoDeImportacion? Exito, DetalleProblema? Error)> ConfirmarImportacionAsync(
        IReadOnlyList<RenglonDeImportacion> renglones, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync(
            "api/productos/importar/confirmar", new { renglones }, ct);

        return await LeerAsync<ResultadoDeImportacion>(respuesta, ct);
    }

    public static string RutaDeExportacion(bool? activos)
        => $"api/productos/exportar" + (activos is { } a ? $"?activos={(a ? "true" : "false")}" : "");

    public static string RutaDePlantilla() => "api/productos/plantilla-csv";

    private static async Task<(T? Exito, DetalleProblema? Error)> LeerAsync<T>(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<T>(ct), null)
            : (default, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
}
