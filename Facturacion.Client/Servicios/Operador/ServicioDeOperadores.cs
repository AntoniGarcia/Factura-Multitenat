using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Client.Servicios.Operador;

/// <summary>Cliente HTTP para la API de operadores del panel.</summary>
public sealed class ServicioDeOperadores(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.ApiOperador);

    public async Task<PaginaDeOperadores> ListarAsync(
        string? texto, bool? activos, int pagina, int tamano, CancellationToken ct = default)
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

        var url = $"/api/operador/operadores?{string.Join("&", query)}";
        var respuesta = await Cliente.GetFromJsonAsync<PaginaDeOperadores>(url, ct);

        return respuesta ?? new PaginaDeOperadores([], 0);
    }

    public async Task<OperadorDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<OperadorDto>($"/api/operador/operadores/{id}", ct);

    public async Task<(OperadorDto? Exito, DetalleProblema? Error)> CrearAsync(
        PeticionGuardarOperador peticion, string contrasenaOperador, CancellationToken ct = default)
    {
        var wrapper = new { peticion, contrasenaOperador };
        using var respuesta = await Cliente.PostAsJsonAsync("/api/operador/operadores", wrapper, ct);

        return await LeerAsync<OperadorDto>(respuesta, ct);
    }

    public async Task<(OperadorDto? Exito, DetalleProblema? Error)> ActualizarAsync(
        Guid id, PeticionGuardarOperador peticion, string contrasenaOperador, CancellationToken ct = default)
    {
        var wrapper = new { peticion, contrasenaOperador };
        using var respuesta = await Cliente.PutAsJsonAsync($"/api/operador/operadores/{id}", wrapper, ct);

        return await LeerAsync<OperadorDto>(respuesta, ct);
    }

    public async Task<(OperadorDto? Exito, DetalleProblema? Error)> CambiarPermisosAsync(
        Guid id, PeticionPermisosOperador peticion, string contrasenaOperador, CancellationToken ct = default)
    {
        var wrapper = new { peticion, contrasenaOperador };
        using var respuesta = await Cliente.PostAsJsonAsync($"/api/operador/operadores/{id}/permisos", wrapper, ct);

        return await LeerAsync<OperadorDto>(respuesta, ct);
    }

    public async Task<(OperadorDto? Exito, DetalleProblema? Error)> CambiarActivoAsync(
        Guid id, PeticionCambiarActivoOperador peticion, string contrasenaOperador, CancellationToken ct = default)
    {
        var wrapper = new { peticion, contrasenaOperador };
        using var respuesta = await Cliente.PostAsJsonAsync($"/api/operador/operadores/{id}/activo", wrapper, ct);

        return await LeerAsync<OperadorDto>(respuesta, ct);
    }

    public async Task<(bool Exito, DetalleProblema? Error)> CambiarContrasenaAsync(
        Guid id, PeticionContrasenaNuevaDeOperador peticion, string contrasenaOperador, CancellationToken ct = default)
    {
        var wrapper = new { peticion, contrasenaOperador };
        using var respuesta = await Cliente.PostAsJsonAsync($"/api/operador/operadores/{id}/contrasena", wrapper, ct);

        if (respuesta.IsSuccessStatusCode)
            return (true, null);

        return (false, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
    }

    private static async Task<(T? Exito, DetalleProblema? Error)> LeerAsync<T>(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<T>(ct), null)
            : (default, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
}