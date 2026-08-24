using Facturacion.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Infra.Idempotencia;

/// <summary>
/// Borra cada hora las claves de idempotencia vencidas. Sin esto, la tabla que guarda la
/// respuesta de cada cobro crece para siempre y la ventana de 24 horas de ARQUITECTURA.md §4 se
/// vuelve permanente.
/// </summary>
public sealed class PurgaDeClavesIdempotencia(
    IServiceScopeFactory fabricaDeAmbitos,
    ILogger<PurgaDeClavesIdempotencia> registro) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var temporizador = new PeriodicTimer(Intervalo);

        try
        {
            // Espera antes de la primera pasada: al arrancar, la base puede no estar lista
            // todavía y no hay nada urgente que purgar.
            while (await temporizador.WaitForNextTickAsync(ct))
            {
                try
                {
                    await Purgar(ct);
                }
                catch (Exception excepcion)
                {
                    // Un fallo de la purga no puede tirar la aplicación: se registra y se
                    // reintenta en la siguiente vuelta.
                    registro.LogError(excepcion, "Falló la purga de claves de idempotencia");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Apagado normal de la aplicación. Sin este catch, el host lo trata como una
            // excepción no controlada de un servicio en segundo plano y registra un fatal
            // en cada cierre.
            registro.LogDebug("Purga de idempotencia detenida por apagado de la aplicación");
        }
    }

    private async Task Purgar(CancellationToken ct)
    {
        using var ambito = fabricaDeAmbitos.CreateScope();
        var baseDeDatos = ambito.ServiceProvider.GetRequiredService<AppDbContext>();

        // IgnoreQueryFilters justificado: la purga corre fuera de una petición, así que no
        // hay empresa activa y el filtro global no devolvería ningún renglón. Barre todas
        // las empresas a propósito, y solo por fecha de vencimiento.
        var borradas = await baseDeDatos.ClavesIdempotencia
            .IgnoreQueryFilters()
            .Where(c => c.ExpiraUtc <= DateTime.UtcNow)
            .ExecuteDeleteAsync(ct);

        if (borradas > 0)
            registro.LogInformation("Purga de idempotencia: {Borradas} claves vencidas", borradas);
    }
}
