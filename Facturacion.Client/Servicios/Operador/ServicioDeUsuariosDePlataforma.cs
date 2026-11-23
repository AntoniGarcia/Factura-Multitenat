using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Client.Servicios.Operador;

/// <summary>Llama a <c>/api/operador/usuarios</c>: soporte sobre los accesos de los clientes.</summary>
public sealed class ServicioDeUsuariosDePlataforma(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.ApiOperador);

    public async Task<PaginaDeUsuariosDePlataforma> ListarAsync(
        string? texto, Guid? cuentaId, int pagina, int tamano, CancellationToken ct = default)
    {
        var ruta = $"api/operador/usuarios?pagina={pagina}&tamano={tamano}";

        if (!string.IsNullOrWhiteSpace(texto)) ruta += $"&texto={Uri.EscapeDataString(texto)}";
        if (cuentaId is { } id) ruta += $"&cuentaId={id}";

        return await Cliente.GetFromJsonAsync<PaginaDeUsuariosDePlataforma>(ruta, ct)
               ?? new PaginaDeUsuariosDePlataforma([], 0);
    }

    public async Task<DetalleProblema?> RestablecerContrasenaAsync(
        Guid usuarioId, string contrasenaNueva, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync(
            $"api/operador/usuarios/{usuarioId}/contrasena",
            new PeticionRestablecerContrasenaDeUsuario(contrasenaNueva), ct);

        return respuesta.IsSuccessStatusCode
            ? null
            : await respuesta.LeerProblemaAsync(ct);
    }

    public async Task<DetalleProblema?> CambiarActivoAsync(
        Guid usuarioId, bool activo, string? motivo, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync(
            $"api/operador/usuarios/{usuarioId}/activo",
            new PeticionCambiarActivoDeUsuario(activo, motivo), ct);

        return respuesta.IsSuccessStatusCode
            ? null
            : await respuesta.LeerProblemaAsync(ct);
    }

    public async Task<DetalleProblema?> EliminarAccesoAsync(
        Guid usuarioId, Guid empresaId, string motivo, CancellationToken ct = default)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Delete,
            $"api/operador/usuarios/{usuarioId}/empresas/{empresaId}/acceso")
        {
            Content = JsonContent.Create(new PeticionEliminarAccesoDeUsuario(motivo))
        };

        using var respuesta = await Cliente.SendAsync(peticion, ct);

        return respuesta.IsSuccessStatusCode
            ? null
            : await respuesta.LeerProblemaAsync(ct);
    }

    public async Task<DetalleProblema?> CambiarCorreoAsync(
        Guid usuarioId, string correoNuevo, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync(
            $"api/operador/usuarios/{usuarioId}/correo",
            new PeticionCambiarCorreoDeUsuario(correoNuevo), ct);

        return respuesta.IsSuccessStatusCode
            ? null
            : await respuesta.LeerProblemaAsync(ct);
    }
}
