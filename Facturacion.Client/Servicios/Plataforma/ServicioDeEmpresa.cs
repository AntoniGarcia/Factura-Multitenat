using System.Net.Http.Json;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>
/// Llama a los endpoints de <c>/api/empresa</c> y <c>/api/series</c>. Ninguno recibe
/// identificador de empresa: la empresa es la del token (ARQUITECTURA.md §4).
/// </summary>
public sealed class ServicioDeEmpresa(IHttpClientFactory fabrica)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    public Task<EmpresaDto?> ObtenerAsync(CancellationToken ct = default)
        => Cliente.GetFromJsonAsync<EmpresaDto>("api/empresa", ct);

    public async Task<(RespuestaGuardarEmpresa? Exito, DetalleProblema? Error)> GuardarAsync(
        PeticionGuardarEmpresa peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync("api/empresa", peticion, ct);
        return await LeerAsync<RespuestaGuardarEmpresa>(respuesta, ct);
    }

    /// <summary>Alta de una empresa emisora. Solo exige sesión: ver ServicioDeEmpresa.CrearAsync.</summary>
    public async Task<(EmpresaDto? Exito, DetalleProblema? Error)> CrearAsync(
        PeticionCrearEmpresa peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync("api/empresa", peticion, ct);
        return await LeerAsync<EmpresaDto>(respuesta, ct);
    }

    public Task<ConfiguracionEmpresaDto?> ObtenerConfiguracionAsync(CancellationToken ct = default)
        => Cliente.GetFromJsonAsync<ConfiguracionEmpresaDto>("api/empresa/configuracion", ct);

    public async Task<(ConfiguracionEmpresaDto? Exito, DetalleProblema? Error)> GuardarConfiguracionAsync(
        ConfiguracionEmpresaDto peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync("api/empresa/configuracion", peticion, ct);
        return await LeerAsync<ConfiguracionEmpresaDto>(respuesta, ct);
    }

    public async Task<IReadOnlyList<CertificadoCsdDto>> ListarCertificadosAsync(CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<IReadOnlyList<CertificadoCsdDto>>("api/empresa/certificados", ct) ?? [];

    public async Task<(CertificadoCsdDto? Exito, DetalleProblema? Error)> CargarCertificadoAsync(
        Stream cer, string nombreCer, Stream key, string nombreKey, string contrasena, CancellationToken ct = default)
    {
        using var cuerpo = new MultipartFormDataContent
        {
            { new StreamContent(cer), "cer", nombreCer },
            { new StreamContent(key), "key", nombreKey },
            { new StringContent(contrasena), "contrasena" }
        };

        using var respuesta = await Cliente.PostAsync("api/empresa/certificados", cuerpo, ct);
        return await LeerAsync<CertificadoCsdDto>(respuesta, ct);
    }

    public async Task<DetalleProblema?> SubirLogoAsync(
        Stream archivo, string nombre, CancellationToken ct = default)
    {
        using var cuerpo = new MultipartFormDataContent { { new StreamContent(archivo), "archivo", nombre } };

        using var respuesta = await Cliente.PostAsync("api/empresa/logo", cuerpo, ct);

        return respuesta.IsSuccessStatusCode
            ? null
            : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }

    public async Task QuitarLogoAsync(CancellationToken ct = default)
        => await Cliente.DeleteAsync("api/empresa/logo", ct);

    public async Task<IReadOnlyList<SerieDto>> ListarSeriesAsync(CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<IReadOnlyList<SerieDto>>("api/series", ct) ?? [];

    /// <summary>Series activas para elegir al emitir, sin folio actual. Solo exige sesión.</summary>
    public async Task<IReadOnlyList<SerieParaEmisionDto>> ListarSeriesActivasAsync(
        string tipoComprobante, CancellationToken ct = default)
        => await Cliente.GetFromJsonAsync<IReadOnlyList<SerieParaEmisionDto>>(
            $"api/series/activas?tipoComprobante={Uri.EscapeDataString(tipoComprobante)}", ct) ?? [];

    public async Task<(SerieDto? Exito, DetalleProblema? Error)> CrearSerieAsync(
        PeticionGuardarSerie peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync("api/series", peticion, ct);
        return await LeerAsync<SerieDto>(respuesta, ct);
    }

    public async Task<(SerieDto? Exito, DetalleProblema? Error)> ActualizarSerieAsync(
        Guid id, PeticionGuardarSerie peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync($"api/series/{id}", peticion, ct);
        return await LeerAsync<SerieDto>(respuesta, ct);
    }

    /// <summary>
    /// Separa el éxito del Problem Details para que la pantalla pueda pintar el error con
    /// <c>PanelErrores</c> y su <c>traceId</c>, en vez de un mensaje genérico.
    /// </summary>
    private static async Task<(T? Exito, DetalleProblema? Error)> LeerAsync<T>(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<T>(ct), null)
            : (default, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
}
