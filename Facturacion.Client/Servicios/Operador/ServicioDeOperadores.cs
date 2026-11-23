using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Client.Servicios.Operador;

/// <summary>Cliente HTTP para la API de operadores del panel.</summary>
public sealed class ServicioDeOperadores(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.ApiOperador);

    public async Task<DetalleProblema?> EliminarAsync(Guid id, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsync(
            $"api/operador/operadores/{id}/eliminar", content: null, ct);
        return respuesta.IsSuccessStatusCode ? null : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }

    public async Task<PaginaDeOperadores> ListarAsync(
        string? texto, bool? activos, bool eliminados, int pagina, int tamano, CancellationToken ct = default)
    {
        var query = new List<string>
        {
            $"pagina={pagina}",
            $"tamano={tamano}"
        };

        if (!string.IsNullOrWhiteSpace(texto))
            query.Add($"texto={Uri.EscapeDataString(texto)}");

        if (activos is not null)
            query.Add($"activos={activos.Value.ToString().ToLowerInvariant()}");
        if (eliminados)
            query.Add("eliminados=true");

        var url = $"/api/operador/operadores?{string.Join("&", query)}";
        var respuesta = await Cliente.GetFromJsonAsync<PaginaDeOperadores>(url, ct);

        return respuesta ?? new PaginaDeOperadores([], 0);
    }

    public async Task<OperadorDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<OperadorDto>($"/api/operador/operadores/{id}", ct);

    public async Task<IReadOnlyList<ActividadDeOperadorDto>> ActividadAsync(Guid id, int pagina, CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<List<ActividadDeOperadorDto>>($"/api/operador/operadores/{id}/actividad?pagina={pagina}", ct) ?? [];

    public async Task<int> LimpiarHistorialDeSesionesAsync(Guid id, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.DeleteAsync($"/api/operador/operadores/{id}/actividad/sesiones", ct);
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<int>(ct);
    }

    public async Task<(OperadorDto? Exito, DetalleProblema? Error)> CrearAsync(
        PeticionGuardarOperador peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync("/api/operador/operadores", peticion, ct);

        return await LeerAsync<OperadorDto>(respuesta, ct);
    }

    public async Task<(OperadorDto? Exito, DetalleProblema? Error)> ActualizarAsync(
        Guid id, PeticionGuardarOperador peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync($"/api/operador/operadores/{id}", peticion, ct);

        return await LeerAsync<OperadorDto>(respuesta, ct);
    }

    public async Task<(OperadorDto? Exito, DetalleProblema? Error)> CambiarActivoAsync(
        Guid id, PeticionCambiarActivoOperador peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync($"/api/operador/operadores/{id}/activo", peticion, ct);

        return await LeerAsync<OperadorDto>(respuesta, ct);
    }

    private static async Task<(T? Exito, DetalleProblema? Error)> LeerAsync<T>(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<T>(ct), null)
            : (default, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
}
