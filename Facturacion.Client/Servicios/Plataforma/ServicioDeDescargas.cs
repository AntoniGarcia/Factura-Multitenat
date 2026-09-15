using Microsoft.JSInterop;

namespace Facturacion.Client.Servicios.Plataforma;

public sealed record ArchivoParaDescarga(string Nombre, string TipoContenido, byte[] Contenido);

/// <summary>
/// Entrega al navegador un archivo que ya descargó un cliente HTTP autenticado. No abre la
/// ruta directamente porque esa navegación perdería el access token que vive en memoria.
/// </summary>
public sealed class ServicioDeDescargas(IJSRuntime js)
{
    private Task<IJSObjectReference>? _modulo;

    public async ValueTask GuardarAsync(ArchivoParaDescarga archivo)
    {
        using var flujo = new MemoryStream(archivo.Contenido, writable: false);
        using var referencia = new DotNetStreamReference(flujo);

        var modulo = await ModuloAsync();
        await modulo.InvokeVoidAsync(
            "guardarDescarga", archivo.Nombre, archivo.TipoContenido, referencia);
    }

    public async ValueTask<string> CrearVistaPreviaAsync(ArchivoParaDescarga archivo)
    {
        using var flujo = new MemoryStream(archivo.Contenido, writable: false);
        using var referencia = new DotNetStreamReference(flujo);

        var modulo = await ModuloAsync();
        return await modulo.InvokeAsync<string>(
            "crearVistaPrevia", archivo.TipoContenido, referencia);
    }

    public async ValueTask LiberarVistaPreviaAsync(string url)
    {
        var modulo = await ModuloAsync();
        await modulo.InvokeVoidAsync("liberarVistaPrevia", url);
    }

    private Task<IJSObjectReference> ModuloAsync()
        => _modulo ??= js.InvokeAsync<IJSObjectReference>("import", "./descargas.js").AsTask();
}
