using System.Net.Http.Json;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Transporte;

namespace Facturacion.Client.Servicios.Plataforma;

public sealed class ServicioDeTransporte(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    public Task<List<VehiculoDto>?> VehiculosAsync(bool? activos, CancellationToken ct = default)
        => Cliente.GetFromJsonAsync<List<VehiculoDto>>($"api/transporte/vehiculos{Filtro(activos)}", ct);

    public Task<List<FiguraTransporteDto>?> FigurasAsync(bool? activos, CancellationToken ct = default)
        => Cliente.GetFromJsonAsync<List<FiguraTransporteDto>>($"api/transporte/figuras{Filtro(activos)}", ct);

    public Task<DomicilioPorCodigoPostalDto?> ResolverDomicilioPorCodigoPostalAsync(string codigoPostal, CancellationToken ct = default)
        => Cliente.GetFromJsonAsync<DomicilioPorCodigoPostalDto>($"api/catalogos/codigo-postal/{Uri.EscapeDataString(codigoPostal)}/domicilio", ct);

    public Task<List<ClaveSatDto>?> MunicipiosAsync(string estado, CancellationToken ct = default)
        => Cliente.GetFromJsonAsync<List<ClaveSatDto>>($"api/catalogos/estados/{Uri.EscapeDataString(estado)}/municipios", ct);

    public Task<List<ClaveSatDto>?> CodigosPostalesAsync(string estado, string municipio, string texto, CancellationToken ct = default)
        => Cliente.GetFromJsonAsync<List<ClaveSatDto>>($"api/catalogos/estados/{Uri.EscapeDataString(estado)}/municipios/{Uri.EscapeDataString(municipio)}/codigos-postales?texto={Uri.EscapeDataString(texto)}", ct);

    public Task<(VehiculoDto? Exito, DetalleProblema? Error)> GuardarAsync(Guid? id, PeticionGuardarVehiculo peticion, CancellationToken ct = default)
        => EnviarAsync<VehiculoDto>(id is null ? Cliente.PostAsJsonAsync("api/transporte/vehiculos", peticion, ct) : Cliente.PutAsJsonAsync($"api/transporte/vehiculos/{id}", peticion, ct), ct);

    public Task<(FiguraTransporteDto? Exito, DetalleProblema? Error)> GuardarAsync(Guid? id, PeticionGuardarFiguraTransporte peticion, CancellationToken ct = default)
        => EnviarAsync<FiguraTransporteDto>(id is null ? Cliente.PostAsJsonAsync("api/transporte/figuras", peticion, ct) : Cliente.PutAsJsonAsync($"api/transporte/figuras/{id}", peticion, ct), ct);

    public Task<(VehiculoDto? Exito, DetalleProblema? Error)> CambiarActivoVehiculoAsync(Guid id, bool activo, CancellationToken ct = default)
        => EnviarAsync<VehiculoDto>(Cliente.PostAsJsonAsync($"api/transporte/vehiculos/{id}/activo", new { activo }, ct), ct);

    public Task<(FiguraTransporteDto? Exito, DetalleProblema? Error)> CambiarActivoFiguraAsync(Guid id, bool activo, CancellationToken ct = default)
        => EnviarAsync<FiguraTransporteDto>(Cliente.PostAsJsonAsync($"api/transporte/figuras/{id}/activo", new { activo }, ct), ct);

    private static string Filtro(bool? activos) => activos is null ? "" : $"?activos={activos.Value.ToString().ToLowerInvariant()}";
    private static async Task<(T? Exito, DetalleProblema? Error)> EnviarAsync<T>(Task<HttpResponseMessage> envio, CancellationToken ct)
    {
        using var respuesta = await envio;
        return respuesta.IsSuccessStatusCode ? (await respuesta.Content.ReadFromJsonAsync<T>(ct), null) : (default, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
    }
}
