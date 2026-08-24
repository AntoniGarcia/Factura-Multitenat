using System.Net.Http.Json;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.JSInterop;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>
/// El tema vive en el perfil del usuario, en el servidor, no en el navegador (ARQUITECTURA.md §8):
/// el contador que cambia de máquina tiene que encontrar el suyo. Este servicio solo aplica
/// al DOM lo que ya trajo la sesión y avisa al servidor cuando el usuario elige otro.
/// </summary>
public sealed class ServicioDeTema(IJSRuntime js, IHttpClientFactory fabrica)
{
    public string Actual { get; private set; } = Temas.Claro;

    public event Action? Cambio;

    /// <summary>
    /// Aplica el tema de la sesión recién resuelta. La llama la puerta de arranque, después
    /// del refresh y antes de renderizar el resto de la aplicación: así no hay un parpadeo
    /// del tema claro por omisión antes de que llegue el de verdad.
    /// </summary>
    public async Task InicializarAsync(string? temaDeLaSesion)
        => await AplicarAsync(Temas.EsValido(temaDeLaSesion) ? temaDeLaSesion! : Temas.Claro);

    /// <summary>Cambio explícito del usuario: se aplica de inmediato y se guarda en el servidor.</summary>
    public async Task CambiarAsync(string tema)
    {
        if (tema == Actual || !Temas.EsValido(tema)) return;

        await AplicarAsync(tema);

        // Optimista a propósito: si la llamada al servidor falla por una red intermitente,
        // el contador ya está trabajando con el tema que eligió. Se reintentará solo la
        // próxima vez que lo cambie; no vale la pena bloquear la interfaz por esto.
        var cliente = fabrica.CreateClient(ClientesHttp.Api);
        await cliente.PutAsJsonAsync("api/perfil/tema", new PeticionCambioTema(tema));
    }

    private async Task AplicarAsync(string tema)
    {
        Actual = tema;
        await js.InvokeVoidAsync("aplicarTema", tema);
        Cambio?.Invoke();
    }
}
