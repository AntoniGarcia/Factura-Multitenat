using System.Net.Http.Json;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>Llama a <c>/api/perfil</c>: nombre, contraseña y tema del usuario que tiene la sesión.</summary>
public sealed class ServicioDePerfil(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    public Task<PerfilDto?> ObtenerAsync(CancellationToken ct = default)
        => Cliente.GetFromJsonAsync<PerfilDto>("api/perfil", ct);

    public async Task<DetalleProblema?> ActualizarNombreAsync(string nombre, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync("api/perfil", new PeticionActualizarPerfil(nombre), ct);
        return respuesta.IsSuccessStatusCode ? null : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }

    public async Task<DetalleProblema?> CambiarContrasenaAsync(
        string contrasenaActual, string contrasenaNueva, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync(
            "api/perfil/contrasena", new PeticionCambiarContrasena(contrasenaActual, contrasenaNueva), ct);

        return respuesta.IsSuccessStatusCode ? null : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }
}
