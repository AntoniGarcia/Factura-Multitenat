namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>
/// Timbres disponibles para mostrar en la barra superior.
/// <para>
/// La interfaz sigue existiendo aunque ya haya implementación real: es lo que mantiene a
/// <c>MainLayout</c> sin conocer el servicio de timbres, y con él la barra superior sin
/// depender de una pantalla concreta. La implementación es
/// <see cref="ServicioDeTimbres"/> desde la fase 7.
/// </para>
/// </summary>
public interface IIndicadorDeTimbres
{
    Task<int> ObtenerDisponiblesAsync();
}
