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
    /// <summary>
    /// Cada cuánto se revisa. Seis horas y no veinticuatro: el costo de la consulta es
    /// despreciable contra una tabla indexada, y con un intervalo más corto un fallo
    /// puntual no implica esperar un día entero para reintentar. Con <see cref="Plazo"/>
    /// de un día, revisar más seguido no borra nada de más.
    /// </summary>
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);

    /// <summary>
    /// Retardo antes de la pasada inicial. Da tiempo a que la base de datos esté arriba si
    /// el servicio arranca antes que ella. No es fatal si falla: el ciclo periódico reintenta.
    /// </summary>
    private static readonly TimeSpan RetardoInicial = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Antigüedad a partir de la cual un borrador vacío se da por abandonado. Un día es
    /// suficiente: el filtro ya exige que nadie haya pulsado «Guardar borrador»
    /// (<c>ModificadoUtc == null</c>), así que no hay captura que perder. El plazo solo
    /// evita borrar el borrador que el usuario acaba de abrir y todavía tiene en pantalla.
    /// </summary>
    private static readonly TimeSpan Plazo = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Pasada inicial: limpia el backlog que se acumuló mientras el servicio estuvo
        // apagado. El retardo es corto (no el intervalo completo) pero suficiente para que
        // la base esté arriba si el servicio arranca antes que ella.
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
            // No es fatal: el ciclo periódico lo reintentará. Si el servicio se reinicia
            // con frecuencia, el siguiente arranque también lo recupera.
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
        var baseDeDatos = ambito.ServiceProvider.GetRequiredService<AppDbContext>();

        var borrador = EstatusComprobante.Borrador.ACadena();
        var limite = DateTime.UtcNow - Plazo;

        // Diagnóstico bajo Debug: tres COUNT con NOT EXISTS sobre la tabla más consultada
        // del sistema no valen la pena en operación normal. Se activan subiendo el nivel
        // de log a Debug cuando haga falta investigar.
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

        // IgnoreQueryFilters justificado: el barrido corre fuera de una petición, así que no
        // hay empresa activa y el filtro global no devolvería ningún renglón. Recorre todas
        // las empresas a propósito.
        //
        // Nota: ExecuteDeleteAsync ejecuta SQL directo y NO dispara las cascadas de EF. Hoy
        // no importa porque el filtro exige !Conceptos.Any(), pero si algún día se relaja,
        // habrá que borrar también Conceptos, ComprobantesRelacionados e IntentosTimbrado.
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