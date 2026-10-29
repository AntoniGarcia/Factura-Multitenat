using System.Net.Http.Json;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>
/// Llama a <c>/api/usuarios</c>. La empresa activa va en el token; las únicas empresas que
/// viajan son las elegidas para el acceso del usuario (AGENTS.md §11).
/// </summary>
public sealed class ServicioDeUsuarios(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    public async Task<DetalleProblema?> EliminarAsync(Guid usuarioId, string contrasena, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync(
            $"api/usuarios/{usuarioId}/eliminar", new PeticionEliminarConContrasena(contrasena), ct);
        return respuesta.IsSuccessStatusCode ? null : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }

    public async Task<UsuariosDeLaCuentaDto> ListarAsync(CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<UsuariosDeLaCuentaDto>("api/usuarios", ct)
           ?? new UsuariosDeLaCuentaDto([], 0, 0, []);

    public async Task<(RespuestaCrearUsuario? Exito, DetalleProblema? Error)> CrearAsync(
        PeticionCrearUsuario peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync("api/usuarios", peticion, ct);
        return await LeerAsync<RespuestaCrearUsuario>(respuesta, ct);
    }

    public async Task<DetalleProblema?> CambiarCorreoAsync(
        Guid usuarioId, string correo, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync(
            $"api/usuarios/{usuarioId}/correo", new PeticionCambiarCorreoUsuario(correo), ct);

        return respuesta.IsSuccessStatusCode ? null : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }

    public async Task<DetalleProblema?> CambiarContrasenaAsync(
        Guid usuarioId, string contrasena, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync(
            $"api/usuarios/{usuarioId}/contrasena", new PeticionCambiarContrasenaUsuario(contrasena), ct);

        return respuesta.IsSuccessStatusCode ? null : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }

    public async Task<DetalleProblema?> ActualizarPermisosAsync(
        Guid usuarioId, IReadOnlyList<string> permisos, IReadOnlyList<Guid> empresas, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync(
            $"api/usuarios/{usuarioId}/permisos", new PeticionActualizarPermisos(permisos, empresas), ct);

        return respuesta.IsSuccessStatusCode ? null : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }

    public async Task<(UsuarioDeLaCuentaDto? Exito, DetalleProblema? Error)> CambiarActivoAsync(
        Guid usuarioId, bool activo, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync(
            $"api/usuarios/{usuarioId}/activo", new PeticionCambiarActivoUsuario(activo), ct);

        return await LeerAsync<UsuarioDeLaCuentaDto>(respuesta, ct);
    }

    private static async Task<(T? Exito, DetalleProblema? Error)> LeerAsync<T>(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<T>(ct), null)
            : (default, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
}
