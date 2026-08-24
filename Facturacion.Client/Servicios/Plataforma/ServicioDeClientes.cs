using System.Net.Http.Json;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>Llama a <c>/api/clientes</c>. La empresa nunca viaja: va en el token.</summary>
public sealed class ServicioDeClientes(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    public async Task<PaginaDeClientes> ListarAsync(
        string? texto, bool? activos, int pagina, int tamano,
        string? orden, bool descendente, CancellationToken ct = default)
    {
        // El filtro de estatus se omite de la URL cuando es "todos": un &activos= vacío
        // lo ata el enlazador del servidor a false, que es justo lo contrario.
        var url = $"api/clientes?pagina={pagina}&tamano={tamano}" +
                  (activos is { } a ? $"&activos={(a ? "true" : "false")}" : "") +
                  $"&descendente={descendente}" +
                  (string.IsNullOrWhiteSpace(texto) ? "" : $"&texto={Uri.EscapeDataString(texto)}") +
                  (string.IsNullOrWhiteSpace(orden) ? "" : $"&orden={Uri.EscapeDataString(orden)}");

        return await Cliente.GetFromJsonAsync<PaginaDeClientes>(url, ct)
               ?? new PaginaDeClientes([], 0);
    }

    public Task<ClienteDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
        => Cliente.GetFromJsonAsync<ClienteDto>($"api/clientes/{id}", ct);

    public async Task<(RespuestaGuardarCliente? Exito, DetalleProblema? Error)> CrearAsync(
        PeticionGuardarCliente peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync("api/clientes", peticion, ct);
        return await LeerAsync<RespuestaGuardarCliente>(respuesta, ct);
    }

    public async Task<(RespuestaGuardarCliente? Exito, DetalleProblema? Error)> ActualizarAsync(
        Guid id, PeticionGuardarCliente peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync($"api/clientes/{id}", peticion, ct);
        return await LeerAsync<RespuestaGuardarCliente>(respuesta, ct);
    }

    public async Task<(ClienteDto? Exito, DetalleProblema? Error)> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync(
            $"api/clientes/{id}/activo", new { activo }, ct);

        return await LeerAsync<ClienteDto>(respuesta, ct);
    }

    /// <summary>Ruta del CSV. La descarga la hace el navegador, no se pasa por memoria del WebAssembly.</summary>
    public static string RutaDeExportacion(bool? activos)
        => $"api/clientes/exportar" + (activos is { } a ? $"?activos={(a ? "true" : "false")}" : "");

    private static async Task<(T? Exito, DetalleProblema? Error)> LeerAsync<T>(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<T>(ct), null)
            : (default, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
}
