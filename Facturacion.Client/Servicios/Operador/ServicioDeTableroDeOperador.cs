using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Client.Servicios.Operador;

/// <summary>Las cifras del negocio y las suscripciones de las cuentas.</summary>
public sealed class ServicioDeTableroDeOperador(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.ApiOperador);

    public async Task<TableroDeOperadorDto?> ObtenerAsync(CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<TableroDeOperadorDto>("api/operador/tablero", ct);

    public async Task<IReadOnlyList<MembresiaDeOperadorDto>> MembresiasAsync(
        Guid cuentaId, CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<IReadOnlyList<MembresiaDeOperadorDto>>(
               $"api/operador/membresias?cuentaId={cuentaId}", ct) ?? [];

    public async Task<(MembresiaDeOperadorDto? Exito, DetalleProblema? Error)> RegistrarMembresiaAsync(
        PeticionRegistrarMembresia peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync("api/operador/membresias", peticion, ct);

        return respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<MembresiaDeOperadorDto>(ct), null)
            : (null, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
    }

    public async Task<DetalleProblema?> CancelarMembresiaAsync(
        Guid membresiaId, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsync(
            $"api/operador/membresias/{membresiaId}/cancelar", content: null, ct);

        return respuesta.IsSuccessStatusCode
            ? null
            : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }
}
