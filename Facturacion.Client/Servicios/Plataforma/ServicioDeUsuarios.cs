using System.Net.Http.Json;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>Llama a <c>/api/usuarios</c> e <c>/api/invitaciones</c>. La empresa nunca viaja: va en el token.</summary>
public sealed class ServicioDeUsuarios(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    public async Task<UsuariosDeLaEmpresaDto> ListarAsync(CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<UsuariosDeLaEmpresaDto>("api/usuarios", ct)
           ?? new UsuariosDeLaEmpresaDto([], [], 3);

    public async Task<(RespuestaInvitar? Exito, DetalleProblema? Error)> InvitarAsync(
        PeticionInvitarUsuario peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync("api/usuarios/invitar", peticion, ct);
        return await LeerAsync<RespuestaInvitar>(respuesta, ct);
    }

    public async Task<DetalleProblema?> ReenviarAsync(Guid invitacionId, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsync($"api/usuarios/invitaciones/{invitacionId}/reenviar", content: null, ct);
        return respuesta.IsSuccessStatusCode ? null : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }

    public async Task<DetalleProblema?> RevocarAsync(Guid invitacionId, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsync($"api/usuarios/invitaciones/{invitacionId}/revocar", content: null, ct);
        return respuesta.IsSuccessStatusCode ? null : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }

    public async Task<DetalleProblema?> ActualizarPermisosAsync(
        Guid usuarioId, IReadOnlyList<string> permisos, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync(
            $"api/usuarios/{usuarioId}/permisos", new PeticionActualizarPermisos(permisos), ct);

        return respuesta.IsSuccessStatusCode ? null : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }

    public async Task<(UsuarioDeEmpresaDto? Exito, DetalleProblema? Error)> CambiarActivoAsync(
        Guid usuarioId, bool activo, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync(
            $"api/usuarios/{usuarioId}/activo", new PeticionCambiarActivoUsuario(activo), ct);

        return await LeerAsync<UsuarioDeEmpresaDto>(respuesta, ct);
    }

    /// <summary>
    /// Para la pantalla pública de aceptar. Se llega aquí sin sesión, así que un token
    /// vencido o ya usado se ve exactamente igual que uno que nunca existió: <c>null</c>.
    /// </summary>
    public async Task<InvitacionPublicaDto?> ObtenerInvitacionAsync(string token, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.GetAsync($"api/invitaciones/{Uri.EscapeDataString(token)}", ct);
        return respuesta.IsSuccessStatusCode ? await respuesta.Content.ReadFromJsonAsync<InvitacionPublicaDto>(ct) : null;
    }

    public async Task<DetalleProblema?> AceptarInvitacionAsync(
        PeticionAceptarInvitacion peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync("api/invitaciones/aceptar", peticion, ct);
        return respuesta.IsSuccessStatusCode ? null : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }

    private static async Task<(T? Exito, DetalleProblema? Error)> LeerAsync<T>(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<T>(ct), null)
            : (default, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
}
