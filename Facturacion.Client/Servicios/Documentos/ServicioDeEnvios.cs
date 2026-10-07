using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;

namespace Facturacion.Client.Servicios.Documentos;

/// <summary>Envío de comprobantes por correo (B6).</summary>
public sealed class ServicioDeEnvios(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    public async Task<(PropuestaDeEnvioDto? Exito, DetalleProblema? Error)> ObtenerPropuestaAsync(
        Guid id, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.GetAsync($"api/documentos/{id}/envio", ct);
        return await LeerAsync<PropuestaDeEnvioDto>(respuesta, ct);
    }

    public async Task<(EnvioDeCorreoDto? Exito, DetalleProblema? Error)> EnviarAsync(
        Guid id, PeticionDeEnvio peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync($"api/documentos/{id}/enviar", peticion, ct);
        return await LeerAsync<EnvioDeCorreoDto>(respuesta, ct);
    }

    private static async Task<(T? Exito, DetalleProblema? Error)> LeerAsync<T>(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<T>(ct), null)
            : (default, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
}
