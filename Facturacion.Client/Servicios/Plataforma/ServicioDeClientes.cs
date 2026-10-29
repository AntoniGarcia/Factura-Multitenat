using System.Net.Http.Json;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>Llama a <c>/api/clientes</c>. La empresa nunca viaja: va en el token.</summary>
public sealed class ServicioDeClientes(IHttpClientFactory fabrica, ServicioDeDescargas descargas)
{
    private HttpClient Cliente => fabrica.CreateClient(ClientesHttp.Api);

    public async Task<PaginaDeClientes> ListarAsync(
        string? texto, bool? activos, Guid? empresa, int pagina, int tamano,
        string? orden, bool descendente, CancellationToken ct = default)
    {
        // El filtro de estatus se omite de la URL cuando es "todos": un &activos= vacío
        // lo ata el enlazador del servidor a false, que es justo lo contrario.
        var url = $"api/clientes?pagina={pagina}&tamano={tamano}" +
                  (activos is { } a ? $"&activos={(a ? "true" : "false")}" : "") +
                  (empresa is { } e ? $"&empresa={e}" : "") +
                  $"&descendente={descendente}" +
                  (string.IsNullOrWhiteSpace(texto) ? "" : $"&texto={Uri.EscapeDataString(texto)}") +
                  (string.IsNullOrWhiteSpace(orden) ? "" : $"&orden={Uri.EscapeDataString(orden)}");

        return await Cliente.GetFromJsonAsync<PaginaDeClientes>(url, ct)
               ?? new PaginaDeClientes([], 0);
    }

    public Task<ClienteDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
        => Cliente.GetFromJsonAsync<ClienteDto>($"api/clientes/{id}", ct);

    public async Task<(RespuestaGuardarCliente? Exito, DetalleProblema? Error)> CrearAsync(
        PeticionGuardarCliente peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync("api/clientes", peticion, ct);
        return await LeerAsync<RespuestaGuardarCliente>(respuesta, ct);
    }

    public async Task<(RespuestaGuardarCliente? Exito, DetalleProblema? Error)> ActualizarAsync(
        Guid id, PeticionGuardarCliente peticion, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PutAsJsonAsync($"api/clientes/{id}", peticion, ct);
        return await LeerAsync<RespuestaGuardarCliente>(respuesta, ct);
    }

    public async Task<(ClienteDto? Exito, DetalleProblema? Error)> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync(
            $"api/clientes/{id}/activo", new { activo }, ct);

        return await LeerAsync<ClienteDto>(respuesta, ct);
    }

    /// <summary>Borra un cliente que nunca se usó. El servidor rechaza al que ya tiene comprobantes.</summary>
    public async Task<DetalleProblema?> EliminarAsync(Guid id, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.DeleteAsync($"api/clientes/{id}", ct);

        return respuesta.IsSuccessStatusCode
            ? null
            : await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct);
    }

    /// <summary>Sube el CSV y devuelve la vista previa. No guarda nada todavía.</summary>
    public async Task<(VistaPreviaDeImportacionDeClientes? Exito, DetalleProblema? Error)> AnalizarCsvAsync(
        Stream archivo, string nombre, CancellationToken ct = default)
    {
        using var cuerpo = new MultipartFormDataContent { { new StreamContent(archivo), "archivo", nombre } };

        using var respuesta = await Cliente.PostAsync("api/clientes/importar/analizar", cuerpo, ct);
        return await LeerAsync<VistaPreviaDeImportacionDeClientes>(respuesta, ct);
    }

    public async Task<(ResultadoDeImportacionDeClientes? Exito, DetalleProblema? Error)> ConfirmarImportacionAsync(
        IReadOnlyList<RenglonDeImportacionDeCliente> renglones, CancellationToken ct = default)
    {
        using var respuesta = await Cliente.PostAsJsonAsync(
            "api/clientes/importar/confirmar", new PeticionConfirmarImportacionDeClientes(renglones), ct);

        return await LeerAsync<ResultadoDeImportacionDeClientes>(respuesta, ct);
    }

    public static string RutaDePlantilla() => "api/clientes/plantilla-csv";

    public Task<DetalleProblema?> DescargarPlantillaAsync(CancellationToken ct = default)
        => descargas.DescargarAsync(RutaDePlantilla(), "plantilla-clientes.csv", ct);

    public Task<DetalleProblema?> ExportarAsync(bool? activos, Guid? empresa = null, CancellationToken ct = default)
        => descargas.DescargarAsync(RutaDeExportacion(activos, empresa), "clientes.csv", ct);

    public static string RutaDeExportacion(bool? activos, Guid? empresa = null)
        => "api/clientes/exportar?" + string.Join("&", new[]
        {
            activos is { } a ? $"activos={(a ? "true" : "false")}" : null,
            empresa is { } e ? $"empresa={e}" : null
        }.Where(p => p is not null));

    private static async Task<(T? Exito, DetalleProblema? Error)> LeerAsync<T>(
        HttpResponseMessage respuesta, CancellationToken ct)
        => respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<T>(ct), null)
            : (default, await respuesta.Content.ReadFromJsonAsync<DetalleProblema>(ct));
}
