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
/// timbrado. Aun así el barrido no se conforma con el estatus y exige las tres
/// señales juntas: sin cliente, sin conceptos y sin <c>ModificadoUtc</c>. La última es la que
/// de verdad decide, porque solo <c>ServicioDeEmision.GuardarAsync</c> la escribe: si viene
/// nula, el usuario jamás pulsó «Guardar borrador» y no hay nada capturado que perder.
///
/// <para><b>Por qué no deja rastro en la bitácora</b></para>
/// La bitácora registra lo que cambia datos fiscales o de facturación. Estas filas no llegaron
/// a tener ninguno: solo la copia del emisor, la fecha y el usuario que abrió la pantalla.
/// Anotar cada una sería cambiar miles de renglones basura por miles de renglones de bitácora.
/// El conteo va al log, como en <c>PurgaDeClavesIdempotencia</c>.
///
/// <para><b>Por qué hay una pasada inicial y no solo el ciclo periódico</b></para>
/// Un <see cref="PeriodicTimer"/> no dispara al arrancar: su primer tick cae al cumplirse
/// el intervalo. Si el servicio se reinicia con más frecuencia que el intervalo —despliegues,
/// reciclaje del App Pool, contenedores, apagados manuales—, la primera pasada nunca ocurre y
/// el backlog se acumula indefinidamente. La pasada inicial tras un retardo corto hace que el
/// barrido converja al estado deseado en cada arranque, sin importar cuándo fue el último.
/// </summary>
public sealed class BarridoDeBorradoresHuerfanos(
    IServiceScopeFactory fabricaDeAmbitos,
    ILogger<BarridoDeBorradoresHuerfanos> registro) : BackgroundService
{
    // Con un Plazo de un día, revisar cada seis horas no borra nada de más y reintenta pronto.
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);

    // Da tiempo a que la base esté arriba si el servicio arranca antes que ella.
    private static readonly TimeSpan RetardoInicial = TimeSpan.FromMinutes(2);

    // Solo evita borrar el borrador que el usuario acaba de abrir: el filtro ya exige que nunca
    // se haya guardado (ModificadoUtc nulo).
    private static readonly TimeSpan Plazo = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Pasada inicial: limpia lo acumulado mientras el servicio estuvo apagado.
        try
        {
            await Task.Delay(RetardoInicial, ct);
            await Barrer(ct);
        }
        catch (OperationCanceledException)
        {
            registro.LogDebug("Barrido de borradores huérfanos detenido durante el arranque");
            return;
        }
        catch (Exception excepcion)
        {
            // No es fatal: el ciclo periódico lo reintenta.
            registro.LogError(excepcion, "Falló el barrido inicial de borradores huérfanos");
        }

        using var temporizador = new PeriodicTimer(Intervalo);

        try
        {
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
        using var usoBase = ambito.ServiceProvider.GetRequiredService<Facturacion.Server.Infra.ActualizacionBase.PausaDeBase>().Tomar();
        if (usoBase is null) return;
        var baseDeDatos = ambito.ServiceProvider.GetRequiredService<AppDbContext>();

        var borrador = EstatusComprobante.Borrador.ACadena();
        var limite = DateTime.UtcNow - Plazo;

        // Diagnóstico solo con log en Debug: son tres COUNT sobre la tabla más consultada.
        if (registro.IsEnabled(LogLevel.Debug))
        {
            var total = await baseDeDatos.Comprobantes
                .IgnoreQueryFilters()
                .CountAsync(c => c.Estatus == borrador, ct);

            var candidatos = await baseDeDatos.Comprobantes
                .IgnoreQueryFilters()
                .CountAsync(c => c.Estatus == borrador
                                 && c.ModificadoUtc == null
                                 && c.ClienteId == null
                                 && !c.Conceptos.Any(), ct);

            var cumplenPlazo = await baseDeDatos.Comprobantes
                .IgnoreQueryFilters()
                .CountAsync(c => c.Estatus == borrador
                                 && c.ModificadoUtc == null
                                 && c.ClienteId == null
                                 && !c.Conceptos.Any()
                                 && c.CreadoUtc <= limite, ct);

            registro.LogDebug(
                "Barrido de borradores: {Total} totales, {Candidatos} vacíos sin modificar, {CumplenPlazo} con más de {Plazo}",
                total, candidatos, cumplenPlazo, Plazo);
        }

        /// IgnoreQueryFilters: corre fuera de una petición, sin empresa activa, y recorre todas.
        /// ExecuteDeleteAsync no dispara las cascadas de EF; hoy no importa porque el filtro exige
        /// que no haya conceptos, pero si se relaja hay que borrar también lo relacionado.
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
