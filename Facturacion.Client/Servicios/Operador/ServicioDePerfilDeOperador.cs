using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Client.Servicios.Operador;

/// <summary>Llama a <c>/api/operador/perfil</c>: nombre, correo y contraseña del operador.</summary>
public sealed class ServicioDePerfilDeOperador(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.ApiOperador);

    public async Task<(SesionDeOperadorDto? Exito, DetalleProblema? Error)> ActualizarNombreAsync(
        string nombre, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync(
            "api/operador/perfil", new PeticionActualizarPerfilOperador(nombre), ct);

        return await LeerAsync<SesionDeOperadorDto>(respuesta, ct);
    }

    public async Task<(SesionDeOperadorDto? Exito, DetalleProblema? Error)> CambiarCorreoAsync(
        string correo, string contrasenaActual, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync(
            "api/operador/perfil/correo",
            new PeticionCambiarCorreoOperador(correo, contrasenaActual), ct);

        return await LeerAsync<SesionDeOperadorDto>(respuesta, ct);
    }

    /// <summary>
    /// Cambia la contraseña. Al lograrlo el servidor cierra todas las sesiones, incluida esta:
    /// quien llame debe mandar al operador a iniciar sesión otra vez.
    /// </summary>
    public async Task<DetalleProblema?> CambiarContrasenaAsync(
        string actual, string nueva, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync(
            "api/operador/perfil/contrasena",
            new PeticionCambiarContrasenaOperador(actual, nueva), ct);

        return respuesta.IsSuccessStatusCode
            ? null
            : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }

    private static async Task<(T? Exito, DetalleProblema? Error)> LeerAsync<T>(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<T>(ct), null)
            : (default, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
}
