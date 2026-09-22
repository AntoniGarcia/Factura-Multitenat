using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Notaria;

namespace Facturacion.Client.Servicios.Documentos;

/// <summary>Configura el perfil notarial de la empresa activa; la licencia se valida en el servidor.</summary>
public sealed class ServicioDeNotaria(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    public async Task<(ConfiguracionNotarioDto? Exito, DetalleProblema? Error)> ObtenerAsync(CancellationToken ct = default)
    {
        using var respuesta = await Cliente.GetAsync("api/notaria/configuracion", ct);
        return await LeerAsync(respuesta, ct);
    }

    public async Task<(ConfiguracionNotarioDto? Exito, DetalleProblema? Error)> GuardarAsync(
        PeticionGuardarConfiguracionNotario peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync("api/notaria/configuracion", peticion, ct);
        return await LeerAsync(respuesta, ct);
    }

    public async Task<(DatosNotariaDto? Exito, DetalleProblema? Error)> ObtenerParaComprobanteAsync(
        Guid comprobanteId, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.GetAsync($"api/notaria/comprobantes/{comprobanteId}", ct);
        return await LeerDatosAsync(respuesta, ct);
    }

    public async Task<(DatosNotariaDto? Exito, DetalleProblema? Error)> GuardarParaComprobanteAsync(
        Guid comprobanteId, PeticionGuardarDatosNotaria peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync($"api/notaria/comprobantes/{comprobanteId}", peticion, ct);
        return await LeerDatosAsync(respuesta, ct);
    }

    public async Task<(PartesNotarialesDto? Exito, DetalleProblema? Error)> ObtenerPartesAsync(
        Guid comprobanteId, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.GetAsync($"api/notaria/comprobantes/{comprobanteId}/partes", ct);
        return await LeerPartesAsync(respuesta, ct);
    }

    public async Task<(PartesNotarialesDto? Exito, DetalleProblema? Error)> GuardarPartesAsync(
        Guid comprobanteId, PeticionGuardarPartesNotariales peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync($"api/notaria/comprobantes/{comprobanteId}/partes", peticion, ct);
        return await LeerPartesAsync(respuesta, ct);
    }

    public async Task<(string? Exito, DetalleProblema? Error)> ValidarXmlAsync(
        Guid comprobanteId, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsync($"api/notaria/comprobantes/{comprobanteId}/validar-xml", null, ct);
        return respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<string>(ct), null)
            : (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
    }

    private static async Task<(ConfiguracionNotarioDto? Exito, DetalleProblema? Error)> LeerAsync(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<ConfiguracionNotarioDto>(ct), null)
            : (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));

    private static async Task<(DatosNotariaDto? Exito, DetalleProblema? Error)> LeerDatosAsync(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<DatosNotariaDto>(ct), null)
            : (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));

    private static async Task<(PartesNotarialesDto? Exito, DetalleProblema? Error)> LeerPartesAsync(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<PartesNotarialesDto>(ct), null)
            : (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
}
