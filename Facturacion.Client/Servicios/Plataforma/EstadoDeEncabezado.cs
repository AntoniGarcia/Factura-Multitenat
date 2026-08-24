namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>
/// El título de la página que se muestra ahora mismo. Las páginas no pintan su propio
/// encabezado —lo pinta <c>MainLayout</c>— así que lo declaran por parámetro con
/// <see cref="Componentes.Comunes.TituloDePagina"/> y este servicio es el mensajero entre
/// la página y el layout (ARQUITECTURA.md §8).
/// </summary>
public sealed class EstadoDeEncabezado
{
    public string? Titulo { get; private set; }

    public event Action? Cambio;

    public void Establecer(string? titulo)
    {
        if (titulo == Titulo) return;

        Titulo = titulo;
        Cambio?.Invoke();
    }
}
