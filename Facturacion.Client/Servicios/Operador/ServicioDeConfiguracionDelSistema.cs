using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Client.Servicios.Operador;

/// <summary>Llama a <c>/api/operador/configuracion</c>: SMTP y nombre del sistema.</summary>
public sealed class ServicioDeConfiguracionDelSistema(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.ApiOperador);

    public async Task<ConfiguracionDelSistemaDto?> ObtenerAsync(CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<ConfiguracionDelSistemaDto>("api/operador/configuracion", ct);

    public async Task<DetalleProblema?> GuardarAsync(
        PeticionGuardarConfiguracionDelSistema peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync("api/operador/configuracion", peticion, ct);

        return respuesta.IsSuccessStatusCode
            ? null
            : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }
}
