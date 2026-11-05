using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;

namespace Facturacion.Client.Servicios.Documentos;

public sealed class ServicioDeNotasCredito(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);
    public async Task<(OrigenNotaCreditoDto? Exito, DetalleProblema? Error)> ConsultarAsync(string serie, int folio)
    {
        using var respuesta = await Cliente.GetAsync($"api/notas-credito/origen?serie={Uri.EscapeDataString(serie)}&folio={folio}");
        return await LeerAsync<OrigenNotaCreditoDto>(respuesta);
    }
    public async Task<(NotaCreditoDto? Exito, DetalleProblema? Error)> CrearAsync(Guid uuid)
    {
        using var respuesta = await Cliente.PostAsJsonAsync("api/notas-credito/borradores", new PeticionCrearNotaCredito(uuid));
        return await LeerAsync<NotaCreditoDto>(respuesta);
    }
    public async Task<(NotaCreditoDto? Exito, DetalleProblema? Error)> ObtenerAsync(Guid id)
    {
        using var respuesta = await Cliente.GetAsync($"api/notas-credito/{id}");
        return await LeerAsync<NotaCreditoDto>(respuesta);
    }
    public async Task<(NotaCreditoDto? Exito, DetalleProblema? Error)> GuardarAsync(Guid id, PeticionGuardarNotaCredito peticion)
    {
        using var respuesta = await Cliente.PutAsJsonAsync($"api/notas-credito/{id}", peticion);
        return await LeerAsync<NotaCreditoDto>(respuesta);
    }
    private static async Task<(T? Exito, DetalleProblema? Error)> LeerAsync<T>(HttpResponseMessage respuesta)
        => respuesta.IsSuccessStatusCode ? (await respuesta.Content.ReadFromJsonAsync<T>(), null)
            : (default, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>());
}
