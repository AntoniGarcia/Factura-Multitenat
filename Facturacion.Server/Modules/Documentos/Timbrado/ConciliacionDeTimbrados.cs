using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Documentos.Pac;
using Facturacion.Server.Modules.Plataforma.Folios;
using Facturacion.Server.Modules.Plataforma.Timbres;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Timbrado;

/// <summary>
/// Saca del limbo a los comprobantes que quedaron en <c>timbrando</c>.
///
/// <para><b>Qué falla sin esto</b></para>
/// El timbrado manda el XML al PAC fuera de toda transacción, y esa llamada puede no volver:
/// se cae la red, se reinicia el servidor, el PAC tarda de más. El comprobante se queda en
/// <c>timbrando</c> y <b>nadie</b> lo mueve de ahí: no se puede marcar error —el PAC quizá
/// sí lo selló, y reemitirlo daría dos folios fiscales para una venta— ni dar por bueno.
/// Sin este proceso, cada corte de red deja una factura que el usuario ve «en proceso» para
/// siempre, con su timbre apartado y su folio consumido.
///
/// <para><b>Cómo lo resuelve sin arriesgar un duplicado</b></para>
/// No reenvía: <b>pregunta</b>, con la misma clave de idempotencia del intento original. Eso
/// es lo que convierte una incógnita en un hecho — el PAC responde si esa clave ya tiene
/// timbre, si la rechazó, o si nunca la vio.
///
/// <para><b>Por qué construye su propio ámbito por empresa</b></para>
/// Corre fuera de una petición, así que no hay empresa en el token y el filtro global no
/// devolvería nada. Busca los pendientes con <c>IgnoreQueryFilters</c> y luego, para cada
/// uno, arma un contexto acotado a <b>su</b> empresa: así el cierre reutiliza exactamente el
/// mismo código que el timbrado normal, con el mismo aislamiento, en vez de una copia que se
/// separaría con el tiempo.
/// </summary>
public sealed class ConciliacionDeTimbrados(
    IServiceScopeFactory fabricaDeAmbitos,
    IConfiguration configuracion,
    ILoggerFactory fabricaDeRegistros,
    IHttpContextAccessor accesor,
    ILogger<ConciliacionDeTimbrados> registro) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Igual que el barrido de reservas: cómodamente mayor que el peor timbrado real, para
    /// no preguntar por uno que todavía está en vuelo de verdad.
    /// </summary>
    private static readonly TimeSpan Plazo = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var temporizador = new PeriodicTimer(Intervalo);

        while (await temporizador.WaitForNextTickAsync(ct))
        {
            try
            {
                await ConciliarAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Un fallo no puede matar el servicio: el siguiente tic lo vuelve a intentar.
                registro.LogError(ex, "La conciliación de timbrados falló en esta pasada.");
            }
        }
    }

    private async Task ConciliarAsync(CancellationToken ct)
    {
        var limite = DateTime.UtcNow - Plazo;

        using var ambito = fabricaDeAmbitos.CreateScope();
        var baseDeDatos = ambito.ServiceProvider.GetRequiredService<AppDbContext>();

        // IgnoreQueryFilters justificado: esto corre sin petición y sin empresa activa, así
        // que el filtro global no devolvería ningún renglón. Recorre todas las empresas a
        // propósito, y cada una se resuelve después en su propio contexto acotado.
        var pendientes = await baseDeDatos.IntentosTimbrado
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(i => i.Resultado == ResultadosDeIntento.EnVuelo && i.IniciadoUtc < limite)
            .Select(i => new { i.Id, i.EmpresaId, i.ComprobanteId, i.ClaveIdempotencia })
            .Take(100)
            .ToListAsync(ct);

        if (pendientes.Count == 0) return;

        registro.LogInformation("Conciliación: {Cuantos} timbrados sin resolver.", pendientes.Count);

        // Sin PAC no hay a quién preguntar. Se avisa y se deja para cuando lo haya: estos
        // comprobantes no se pueden resolver adivinando.
        if (ambito.ServiceProvider.GetService<IProveedorPac>() is not { } pac)
        {
            registro.LogError(
                "Hay {Cuantos} timbrados sin resolver y ningún PAC registrado para consultarlos.",
                pendientes.Count);

            return;
        }

        foreach (var pendiente in pendientes)
        {
            if (ct.IsCancellationRequested) return;

            if (string.IsNullOrEmpty(pendiente.ClaveIdempotencia))
            {
                registro.LogError(
                    "El intento {Intento} no tiene clave de idempotencia; no se puede preguntar por él sin " +
                    "arriesgar un duplicado. Requiere revisión manual.", pendiente.Id);
                continue;
            }

            try
            {
                var respuesta = await pac.ConsultarAsync(pendiente.ClaveIdempotencia, ct);
                await AplicarAsync(pendiente.EmpresaId, pendiente.ComprobanteId, pendiente.Id, respuesta, ct);
            }
            catch (Exception ex)
            {
                registro.LogWarning(
                    ex, "No se pudo conciliar el intento {Intento}; queda para la siguiente pasada.", pendiente.Id);
            }
        }
    }

    private async Task AplicarAsync(
        Guid empresaId, Guid comprobanteId, Guid intentoId, RespuestaDePac respuesta, CancellationToken ct)
    {
        // Sigue sin saberse: se deja como está. Es la respuesta correcta, no un fallo.
        if (respuesta.Resultado == ResultadoDePac.ErrorDeComunicacion) return;

        var tenencia = new ContextoEmpresaFijo(empresaId);

        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .Configurar(configuracion.GetConnectionString("BaseDeDatos"), tenencia)
            .Options;

        await using var baseDeDatos = new AppDbContext(opciones, tenencia);

        var comprobante = await baseDeDatos.Comprobantes
            .FirstOrDefaultAsync(c => c.Id == comprobanteId, ct);

        var intento = await baseDeDatos.IntentosTimbrado
            .FirstOrDefaultAsync(i => i.Id == intentoId, ct);

        if (comprobante is null || intento is null) return;

        // Alguien más lo resolvió mientras tanto: no se toca.
        if (intento.Resultado != ResultadosDeIntento.EnVuelo) return;

        // Sin HttpContext: la bitácora registrará la operación sin IP ni agente, que es lo
        // correcto para algo que no lo hizo una persona sino el propio sistema.
        var bitacora = new ServicioDeBitacora(baseDeDatos, tenencia, accesor);

        var cierre = new CierreDeTimbrado(
            baseDeDatos,
            new ServicioDeFolios(baseDeDatos, tenencia, bitacora),
            new ServicioDeTimbres(baseDeDatos, tenencia, bitacora),
            fabricaDeRegistros.CreateLogger<CierreDeTimbrado>());

        if (respuesta.Resultado == ResultadoDePac.Timbrado)
        {
            await cierre.ConfirmarAsync(comprobante, intento, respuesta, ct);

            registro.LogWarning(
                "Conciliación: el comprobante {Comprobante} sí estaba timbrado en el PAC (UUID {Uuid}). " +
                "Se recuperó sin volver a enviarlo.", comprobanteId, respuesta.Uuid);

            return;
        }

        // Rechazado o desconocido para el PAC: en los dos casos no hay timbre del otro lado,
        // así que se puede cerrar en error y devolver el timbre con seguridad.
        await cierre.RevertirAsync(
            comprobante, intento,
            respuesta.CodigoError ?? "conciliado-sin-timbre",
            respuesta.Mensaje ?? "El PAC no tiene timbre para esta clave.", ct);

        registro.LogWarning(
            "Conciliación: el comprobante {Comprobante} no llegó a timbrarse ({Resultado}). Quedó en error.",
            comprobanteId, respuesta.Resultado);
    }
}
