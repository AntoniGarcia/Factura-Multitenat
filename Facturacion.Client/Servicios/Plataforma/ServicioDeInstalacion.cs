using Microsoft.JSInterop;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>
/// El botón de instalar la PWA, solo visible cuando el navegador dice que se puede
/// (PROMPT-FASES-A.md fase 2). Nunca hay un botón "Instalar" que falle al hacer clic: si el
/// navegador no ofreció el evento —Safari de escritorio, por ejemplo, no lo soporta—,
/// <see cref="PuedeInstalarse"/> se queda en falso y el botón no se dibuja.
/// </summary>
public sealed class ServicioDeInstalacion : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<ServicioDeInstalacion>? _referencia;

    public ServicioDeInstalacion(IJSRuntime js) => _js = js;

    public bool PuedeInstalarse { get; private set; }

    public event Action? Cambio;

    public async Task InicializarAsync()
    {
        _referencia = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("registrarInstalador", _referencia);
    }

    public async Task<bool> InstalarAsync() => await _js.InvokeAsync<bool>("instalarPwa");

    [JSInvokable]
    public void NotificarInstalable()
    {
        PuedeInstalarse = true;
        Cambio?.Invoke();
    }

    [JSInvokable]
    public void NotificarInstalada()
    {
        PuedeInstalarse = false;
        Cambio?.Invoke();
    }

    public ValueTask DisposeAsync()
    {
        _referencia?.Dispose();
        return ValueTask.CompletedTask;
    }
}
