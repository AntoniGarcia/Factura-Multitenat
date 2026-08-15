using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Timbres;

/// <summary>
/// Devuelve al saldo disponible las reservas de timbre que llevan demasiado tiempo sin
/// resolverse.
///
/// <para><b>Qué falla sin esto</b></para>
/// Entre apartar el timbre y confirmarlo hay una llamada al PAC, y esa llamada puede no
/// volver: se cae el proceso, se reinicia el servidor, se corta la red a mitad del timbrado.
/// La reserva se queda en <c>reservado</c> para siempre y ese timbre no lo puede usar nadie:
/// no está disponible, y tampoco se gastó. Un cliente con mala suerte va perdiendo timbres
/// pagados de uno en uno sin que nada lo avise.
///
/// <para><b>Por qué treinta minutos y no cinco</b></para>
/// El plazo tiene que ser cómodamente mayor que el peor timbrado real. Si se devolviera un
/// timbre cuya llamada al PAC todavía está viva, el timbrado podría terminar bien y confirmar
/// después una reserva que ya se devolvió: el saldo habría subido y bajado por caminos
/// distintos y la cuenta dejaría de cuadrar. Media hora es holgado para cualquier PAC y sigue
/// siendo corto para el usuario.
/// </summary>
public sealed class BarridoDeReservasDeTimbre(
    IServiceScopeFactory fabricaDeAmbitos,
    ILogger<BarridoDeReservasDeTimbre> registro) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    /// <summary>Antigüedad a partir de la cual una reserva sin resolver se da por abandonada.</summary>
    private static readonly TimeSpan Plazo = TimeSpan.FromMinutes(30);

    private const string Motivo = "Devuelta por el barrido: la reserva pasó de 30 minutos sin resolverse.";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var temporizador = new PeriodicTimer(Intervalo);

        try
        {
            // Espera antes de la primera pasada: al arrancar, la base puede no estar lista y
            // ninguna reserva ha tenido tiempo de abandonarse.
            while (await temporizador.WaitForNextTickAsync(ct))
            {
                try
                {
                    await Barrer(ct);
                }
                catch (Exception excepcion)
                {
                    // Un fallo del barrido no puede tirar la aplicación: se registra y se
                    // reintenta en la siguiente vuelta.
                    registro.LogError(excepcion, "Falló el barrido de reservas de timbre");
                }
            }
        }
        catch (OperationCanceledException)
        {
            registro.LogDebug("Barrido de reservas de timbre detenido por apagado de la aplicación");
        }
    }

    private async Task Barrer(CancellationToken ct)
    {
        using var ambito = fabricaDeAmbitos.CreateScope();
        var baseDeDatos = ambito.ServiceProvider.GetRequiredService<AppDbContext>();
        var bitacora = ambito.ServiceProvider.GetRequiredService<IServicioDeBitacora>();

        var limite = DateTime.UtcNow - Plazo;

        // IgnoreQueryFilters justificado: el barrido corre fuera de una petición, así que no
        // hay empresa activa y el filtro global no devolvería ningún renglón. Recorre todas
        // las empresas a propósito.
        var abandonadas = await baseDeDatos.ReservasTimbre
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.Estado == EstadosDeReservaTimbre.Reservado && r.CreadaUtc <= limite)
            .Select(r => new { r.Id, r.EmpresaId, r.ComprobanteId, r.CreadaUtc })
            .ToListAsync(ct);

        if (abandonadas.Count == 0) return;

        var devueltas = 0;

        foreach (var reserva in abandonadas)
        {
            // Una por una y no en lote: cada devolución es una transacción corta del mismo
            // procedimiento que usa el timbrado, así que el saldo se mueve por un solo camino.
            // Un UPDATE masivo tendría que reimplementar esa aritmética por segunda vez, y dos
            // implementaciones de la misma cuenta acaban discrepando.
            var parametros = new[]
            {
                new SqlParameter("@EmpresaId", reserva.EmpresaId),
                new SqlParameter("@ReservaId", reserva.Id),
                new SqlParameter("@Motivo", Motivo),
                new SqlParameter("@MomentoUtc", DateTime.UtcNow)
            };

            var filas = await baseDeDatos.Database
                .SqlQueryRaw<ResultadoDeResolucion>(
                    "EXEC dbo.DevolverTimbre @EmpresaId, @ReservaId, @Motivo, @MomentoUtc", parametros)
                .ToListAsync(ct);

            // Falso si el timbrado la resolvió entre la consulta y este momento. No es un
            // error: ganó el timbrado, que es lo correcto.
            if (!filas.Single().Resuelto) continue;

            devueltas++;

            bitacora.Registrar(
                EntidadesDeBitacora.ReservaTimbre, reserva.Id.ToString(),
                AccionesDeBitacora.ReservaAbandonada,
                despues: new { reserva.ComprobanteId, reserva.CreadaUtc, Motivo },
                empresaId: reserva.EmpresaId);
        }

        await baseDeDatos.SaveChangesAsync(ct);

        if (devueltas > 0)
        {
            // A nivel de advertencia y no informativo: que haya reservas abandonadas significa
            // que algún timbrado se murió a la mitad, y eso hay que poder verlo en el log.
            registro.LogWarning(
                "Barrido de timbres: {Devueltas} reservas abandonadas volvieron al saldo disponible",
                devueltas);
        }
    }

    private sealed record ResultadoDeResolucion(bool Resuelto);
}
