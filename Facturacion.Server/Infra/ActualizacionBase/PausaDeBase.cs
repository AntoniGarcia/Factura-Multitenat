using Facturacion.Server.Infra.Errores;

namespace Facturacion.Server.Infra.ActualizacionBase;

public sealed class PausaDeBase
{
    private readonly object _candado = new();
    private int _peticiones;
    private bool _cerrada;
    private TaskCompletionSource _libre = NuevaSenal();
    private static TaskCompletionSource NuevaSenal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool Entrar()
    {
        lock (_candado)
        {
            if (_cerrada) return false;
            if (_peticiones++ == 0) _libre = NuevaSenal();
            return true;
        }
    }

    public void Salir()
    {
        lock (_candado) if (--_peticiones == 0) _libre.TrySetResult();
    }

    public Task CerrarAsync()
    {
        lock (_candado)
        {
            _cerrada = true;
            return _peticiones == 0 ? Task.CompletedTask : _libre.Task;
        }
    }

    public void Abrir() { lock (_candado) _cerrada = false; }

    public IDisposable? Tomar() => Entrar() ? new Uso(this) : null;
    private sealed class Uso(PausaDeBase pausa) : IDisposable
    {
        public void Dispose() => pausa.Salir();
    }

    public static bool EsAccesoDeMantenimiento(PathString ruta)
        => ruta.StartsWithSegments("/api/mantenimiento-base")
            || ruta == "/api/version";

    public async Task AtenderAsync(HttpContext contexto, RequestDelegate siguiente)
    {
        if (!contexto.Request.Path.StartsWithSegments("/api") || EsAccesoDeMantenimiento(contexto.Request.Path))
        { await siguiente(contexto); return; }
        if (!Entrar())
        {
            contexto.Response.Headers.RetryAfter = "10";
            await ResultadosDeError.EscribirProblema(contexto, 503, "base-en-mantenimiento",
                "Actualización de la base", "El sistema está en mantenimiento. Intenta de nuevo más tarde.");
            return;
        }
        try { await siguiente(contexto); }
        finally { Salir(); }
    }
}
