using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Client.Servicios.Operador;

/// <summary>
/// Llama a <c>/api/operador/paquetes</c>. Sale por el cliente del panel, que adjunta el token
/// del proveedor y no el del inquilino.
/// </summary>
public sealed class ServicioDePaquetesDeOperador(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.ApiOperador);

    public async Task<IReadOnlyList<PaqueteDeOperadorDto>> ListarAsync(CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<IReadOnlyList<PaqueteDeOperadorDto>>("api/operador/paquetes", ct)
           ?? [];

    public async Task<PaqueteDeOperadorDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<PaqueteDeOperadorDto>($"api/operador/paquetes/{id}", ct);

    public async Task<(PaqueteDeOperadorDto? Exito, DetalleProblema? Error)> CrearAsync(
        PeticionGuardarPaquete peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync("api/operador/paquetes", peticion, ct);
        return await LeerAsync<PaqueteDeOperadorDto>(respuesta, ct);
    }

    public async Task<(PaqueteDeOperadorDto? Exito, DetalleProblema? Error)> ActualizarAsync(
        Guid id, PeticionGuardarPaquete peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync($"api/operador/paquetes/{id}", peticion, ct);
        return await LeerAsync<PaqueteDeOperadorDto>(respuesta, ct);
    }

    public async Task<(PaqueteDeOperadorDto? Exito, DetalleProblema? Error)> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync(
            $"api/operador/paquetes/{id}/activo", new { Activo = activo }, ct);

        return await LeerAsync<PaqueteDeOperadorDto>(respuesta, ct);
    }

    private static async Task<(T? Exito, DetalleProblema? Error)> LeerAsync<T>(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<T>(ct), null)
            : (default, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
}
