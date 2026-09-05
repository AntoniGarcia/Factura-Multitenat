using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Client.Servicios.Operador;

/// <summary>Llama a <c>/api/operador/cuentas</c>: las cuentas contratantes y su ficha.</summary>
public sealed class ServicioDeClientesDePlataforma(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.ApiOperador);

    public async Task<PaginaDeCuentas> ListarAsync(
        string? texto, int pagina, int tamano, CancellationToken ct = default)
    {
        var ruta = $"api/operador/cuentas?pagina={pagina}&tamano={tamano}";

        if (!string.IsNullOrWhiteSpace(texto)) ruta += $"&texto={Uri.EscapeDataString(texto)}";

        return await Cliente.GetFromJsonAsync<PaginaDeCuentas>(ruta, ct)
               ?? new PaginaDeCuentas([], 0);
    }

    public async Task<DetalleDeCuentaDto?> ObtenerAsync(Guid cuentaId, CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<DetalleDeCuentaDto>($"api/operador/cuentas/{cuentaId}", ct);

    public async Task<FichaDeEmpresaDto?> ObtenerEmpresaAsync(
        Guid cuentaId, Guid empresaId, string? texto = null, CancellationToken ct = default)
    {
        var ruta = $"api/operador/cuentas/{cuentaId}/empresas/{empresaId}";

        if (!string.IsNullOrWhiteSpace(texto)) ruta += $"?texto={Uri.EscapeDataString(texto)}";

        return await Cliente.GetFromJsonAsync<FichaDeEmpresaDto>(ruta, ct);
    }

    public async Task<DetalleProblema?> CambiarActivoEmpresaAsync(
        Guid cuentaId, Guid empresaId, PeticionCambiarActivoDeEmpresa peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync(
            $"api/operador/cuentas/{cuentaId}/empresas/{empresaId}/activo", peticion, ct);

        return respuesta.IsSuccessStatusCode
            ? null
            : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }

    public async Task<DetalleProblema?> CambiarCorreoDeContactoDeCuentaAsync(
        Guid cuentaId, PeticionCambiarCorreoDeContactoDeCuenta peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync(
            $"api/operador/cuentas/{cuentaId}/correo-contacto", peticion, ct);

        return respuesta.IsSuccessStatusCode
            ? null
            : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }

    public async Task<DetalleProblema?> AsignarTimbresAsync(
        Guid cuentaId, Guid empresaId, PeticionAsignarTimbres peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync(
            $"api/operador/cuentas/{cuentaId}/empresas/{empresaId}/timbres", peticion, ct);

        return respuesta.IsSuccessStatusCode
            ? null
            : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }
}
