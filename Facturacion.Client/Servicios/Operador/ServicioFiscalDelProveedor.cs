using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Operador;

namespace Facturacion.Client.Servicios.Operador;

public sealed class ServicioFiscalDelProveedor(IHttpClientFactory fabrica)
{
    private const string Ruta = "api/operador/configuracion/facturacion";
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.ApiOperador);

    public async Task<(ConfiguracionFiscalProveedorDto? Datos, DetalleProblema? Error)> ObtenerAsync()
    {
        using var respuesta = await Cliente.GetAsync(Ruta);
        return await LeerAsync(respuesta);
    }

    public async Task<IReadOnlyList<ClaveSatDto>> RegimenesAsync()
        => await Cliente.GetFromJsonAsync<List<ClaveSatDto>>($"{Ruta}/regimenes") ?? [];

    public async Task<IReadOnlyList<ClaveSatDto>> CodigosPostalesAsync(string texto, CancellationToken ct)
        => await Cliente.GetFromJsonAsync<List<ClaveSatDto>>(
            $"{Ruta}/codigos-postales?texto={Uri.EscapeDataString(texto)}", ct) ?? [];

    public async Task<(ConfiguracionFiscalProveedorDto? Datos, DetalleProblema? Error)> GuardarAsync(PeticionGuardarFiscalProveedor p)
    {
        using var respuesta = await Cliente.PutAsJsonAsync(Ruta, p);
        return await LeerAsync(respuesta);
    }

    public async Task<(ConfiguracionFiscalProveedorDto? Datos, DetalleProblema? Error)> CargarCsdAsync(
        Stream cer, string nombreCer, Stream key, string nombreKey, string contrasena,
        string contrasenaOperador, byte[] version)
    {
        using var contenido = new MultipartFormDataContent
        {
            { new StreamContent(cer), "cer", nombreCer },
            { new StreamContent(key), "key", nombreKey },
            { new StringContent(contrasena), "contrasena" },
            { new StringContent(contrasenaOperador), "contrasenaOperador" },
            { new StringContent(Convert.ToBase64String(version)), "version" }
        };
        using var respuesta = await Cliente.PostAsync($"{Ruta}/certificados", contenido);
        return await LeerAsync(respuesta);
    }

    private static async Task<(ConfiguracionFiscalProveedorDto? Datos, DetalleProblema? Error)> LeerAsync(HttpResponseMessage r)
    {
        if (r.IsSuccessStatusCode) return (await r.Content.ReadFromJsonAsync<ConfiguracionFiscalProveedorDto>(), null);
        if (r.Content.Headers.ContentType?.MediaType is "application/json" or "application/problem+json")
            return (null, await r.Content.ReadFromJsonAsync<DetalleProblema>());
        return (null, new DetalleProblema("fiscal-proveedor", "No se pudo completar la operación", (int)r.StatusCode,
            "Revisa la conexión y el tamaño de los archivos.", null, "", null));
    }
}
