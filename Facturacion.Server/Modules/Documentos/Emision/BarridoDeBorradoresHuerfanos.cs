using Facturacion.Server.Data;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Emision;

/// <summary>
/// Borra los borradores que se crearon al abrir «Nueva factura» y que nadie llegó a capturar.
///
/// <para><b>Qué falla sin esto</b></para>
/// El comprobante se crea al entrar a la pantalla, no al guardar: es lo que permite recargar
/// la página sin perder la captura, y es también lo que hace que abrir la pantalla y cerrarla
/// deje una fila. No consume folio ni timbre —eso lo resuelve bien el timbrado en tres pasos—,
/// pero en un año de uso son miles de renglones basura en la tabla que más se consulta.
///
/// <para><b>Por qué esto sí puede borrar</b></para>
/// «Nada se borra» tiene una única excepción, y es exactamente esta: un borrador nunca
/// timbrado (ARQUITECTURA.md §5). Aun así el barrido no se conforma con el estatus y exige las tres
/// señales juntas: sin cliente, sin conceptos y sin <c>ModificadoUtc</c>. La última es la que
/// de verdad decide, porque solo <c>ServicioDeEmision.GuardarAsync</c> la escribe: si viene
/// nula, el usuario jamás pulsó «Guardar borrador» y no hay nada capturado que perder.
///
/// <para><b>Por qué no deja rastro en la bitácora</b></para>
/// La bitácora registra lo que cambia datos fiscales o de facturación. Estas filas no llegaron
/// a tener ninguno: solo la copia del emisor, la fecha y el usuario que abrió la pantalla.
/// Anotar cada una sería cambiar miles de renglones basura por miles de renglones de bitácora.
/// El conteo va al log, como en <c>PurgaDeClavesIdempotencia</c>.
/// </summary>
public sealed class BarridoDeBorradoresHuerfanos(
    IServiceScopeFactory fabricaDeAmbitos,
    ILogger<BarridoDeBorradoresHuerfanos> registro) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);

    /// <summary>
    /// Antigüedad a partir de la cual un borrador vacío se da por abandonado. Una semana es
    /// holgado a propósito: la fila no estorba a nadie mientras tanto, y el precio de borrar
    /// de más —una captura a medias que alguien pensaba retomar el lunes— es mucho más caro
    /// que el de esperar unos días de más.
    /// </summary>
    private static readonly TimeSpan Plazo = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var temporizador = new PeriodicTimer(Intervalo);

        try
        {
            // Espera antes de la primera pasada: al arrancar, la base puede no estar lista y
            // ningún borrador ha tenido tiempo de abandonarse.
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
                    registro.LogError(excepcion, "Falló el barrido de borradores huérfanos");
                }
            }
        }
        catch (OperationCanceledException)
        {
            registro.LogDebug("Barrido de borradores huérfanos detenido por apagado de la aplicación");
        }
    }

    private async Task Barrer(CancellationToken ct)
    {
        using var ambito = fabricaDeAmbitos.CreateScope();
        var baseDeDatos = ambito.ServiceProvider.GetRequiredService<AppDbContext>();

        var borrador = EstatusComprobante.Borrador.ACadena();
        var limite = DateTime.UtcNow - Plazo;

        // IgnoreQueryFilters justificado: el barrido corre fuera de una petición, así que no
        // hay empresa activa y el filtro global no devolvería ningún renglón. Recorre todas
        // las empresas a propósito.
        var borrados = await baseDeDatos.Comprobantes
            .IgnoreQueryFilters()
            .Where(c => c.Estatus == borrador
                        && c.ModificadoUtc == null
                        && c.ClienteId == null
                        && !c.Conceptos.Any()
                        && c.CreadoUtc <= limite)
            .ExecuteDeleteAsync(ct);

        if (borrados > 0)
            registro.LogInformation("Barrido de borradores: {Borrados} borradores vacíos abandonados", borrados);
    }
}
