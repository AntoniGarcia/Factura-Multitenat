using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Pac;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Timbrado;

/// <summary>
/// Timbra un comprobante en tres pasos.
///
/// <para><b>Por qué tres y no uno</b></para>
/// El PAC puede tardar treinta segundos. Una transacción de base de datos abierta ese tiempo
/// bloquea renglones y tumba el sistema entero bajo carga, así que la llamada al PAC ocurre
/// <b>fuera de toda transacción</b> (CLAUDE.md §5). Eso obliga a partir la operación:
///
/// <list type="number">
///   <item><description>
///     <b>Apartar</b> — transacción corta: se toma folio y timbre, el comprobante pasa a
///     <c>timbrando</c> y se abre un intento con su clave de idempotencia.
///   </description></item>
///   <item><description>
///     <b>Enviar</b> — sin transacción: se arma el XML, se sella y se manda al PAC, con
///     reintentos de espera creciente.
///   </description></item>
///   <item><description>
///     <b>Resolver</b> — transacción corta: se confirma o se revierte.
///   </description></item>
/// </list>
///
/// <para><b>El estado que no se resuelve es el importante</b></para>
/// Si el paso 2 no sabe qué pasó —red caída, tiempo agotado—, el comprobante <b>se queda</b>
/// en <c>timbrando</c> con su intento en vuelo. No se marca error: el PAC pudo haberlo
/// sellado y marcarlo como fallido llevaría a emitirlo dos veces. De ahí lo saca
/// <see cref="ConciliacionDeTimbrados"/>, preguntando con la misma clave.
/// </summary>
public sealed class ServicioDeTimbrado(
    AppDbContext baseDeDatos,
    ServicioDeXmlCfdi xml,
    IServicioFolios folios,
    IServicioTimbres timbres,
    CierreDeTimbrado cierre,
    ILogger<ServicioDeTimbrado> registro,
    IProveedorPac? pac = null)
{
    /// <summary>Esperas entre reintentos. Crecientes: si el PAC va lento, insistir rápido lo empeora.</summary>
    private static readonly TimeSpan[] Esperas = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6)];

    public async Task<Resultado<Comprobante>> TimbrarAsync(Guid comprobanteId, CancellationToken ct)
    {
        // Antes de apartar nada. Si no hay PAC, fallar aquí no cuesta un folio ni un timbre;
        // fallar más adelante sí, y encima dejaría el comprobante en 'timbrando' esperando
        // una conciliación que tampoco tendría a quién preguntar.
        if (pac is null)
        {
            registro.LogError("Se intentó timbrar sin un PAC registrado.");

            return ErrorNegocio.Regla(
                "pac-no-configurado",
                "No hay un proveedor de timbrado configurado. Avisa a soporte: no se consumió " +
                "ningún timbre ni folio.");
        }

        var apartado = await ApartarAsync(comprobanteId, ct);

        if (apartado.EsFallo) return apartado.Error!;

        var (comprobante, intento) = apartado.Valor;

        // ── Paso 2: fuera de toda transacción ───────────────────────────────────────────
        RespuestaDePac respuesta;

        try
        {
            var sellado = await xml.GenerarAsync(comprobante, ct);

            if (sellado.EsFallo)
            {
                await cierre.RevertirAsync(comprobante, intento, sellado.Error!.Codigo, sellado.Error.Mensaje, ct);
                return sellado.Error;
            }

            // El sello y el XML se guardan ANTES de enviar, no al confirmar. Si el proceso
            // muere a mitad de la llamada, la conciliación recupera el comprobante horas
            // después y para entonces ya no hay forma de recalcularlos: el XML se selló con la
            // fecha y el folio de este momento, y con el CSD que estuviera activo entonces.
            // Sin el sello el PDF no puede armar el QR —lleva sus últimos ocho caracteres— y
            // sin el XML la conciliación no tiene qué reenviar para preguntar.
            comprobante.SelloCfd = sellado.Valor.Sello;
            comprobante.NoCertificadoEmisor = sellado.Valor.NoCertificado;
            intento.XmlEnviado = sellado.Valor.Xml;
            await baseDeDatos.SaveChangesAsync(ct);

            respuesta = await EnviarConReintentosAsync(pac, sellado.Valor.Xml, intento.ClaveIdempotencia!, ct);
        }
        catch (Exception ex)
        {
            // Cualquier excepción antes de enviar es nuestra, no del PAC: se puede revertir
            // sin riesgo de duplicar, porque el comprobante nunca salió.
            registro.LogError(ex, "Falló la preparación del timbrado de {Comprobante}.", comprobanteId);
            await cierre.RevertirAsync(comprobante, intento, "error-al-preparar", ex.Message, ct);

            return ErrorNegocio.Regla("error-al-preparar", "No se pudo preparar el comprobante para timbrar.");
        }

        return await ResolverAsync(comprobante, intento, respuesta, ct);
    }

    // ── Paso 1 ──────────────────────────────────────────────────────────────────────────

    private async Task<Resultado<(Comprobante, IntentoTimbrado)>> ApartarAsync(
        Guid comprobanteId, CancellationToken ct)
    {
        var comprobante = await baseDeDatos.Comprobantes
            .Include(c => c.Conceptos).ThenInclude(x => x.Impuestos)
            .Include(c => c.Relacionados)
            // Vacío salvo en los CFDI de pago, donde es todo el contenido del comprobante.
            .Include(c => c.Pagos).ThenInclude(p => p.Documentos).ThenInclude(d => d.Impuestos)
            .FirstOrDefaultAsync(c => c.Id == comprobanteId, ct);

        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("comprobante-no-encontrado", "Ese comprobante no existe.");

        // Solo desde borrador o desde un error ya resuelto. Nunca desde timbrando: eso
        // significaría que hay otro intento en vuelo y timbrar de nuevo lo duplicaría.
        if (comprobante.Estatus is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto(
                "comprobante-no-timbrable",
                $"El comprobante está en '{comprobante.Estatus}' y no se puede timbrar desde ahí.");

        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(ct);

        // El folio solo se toma una vez. Si el comprobante ya trae uno de un intento fallido
        // anterior, se conserva: CLAUDE.md §5 prohíbe reciclarlo, así que tampoco se pide
        // otro — se reintenta con el mismo.
        FolioReservadoDto? folio = null;

        if (comprobante.Folio is null && comprobante.SerieId is { } serieId)
        {
            folio = await folios.ReservarAsync(serieId, ct);
            comprobante.SerieId = folio.SerieId;
            comprobante.Serie = folio.Serie;
            comprobante.Folio = folio.Folio;
        }

        ReservaTimbreDto timbre;

        try
        {
            timbre = await timbres.ReservarAsync(comprobanteId, ct);
        }
        catch (Exception ex)
        {
            await transaccion.RollbackAsync(ct);
            registro.LogWarning(ex, "No se pudo apartar timbre para {Comprobante}.", comprobanteId);

            return ErrorNegocio.Regla("sin-timbres", "No quedan timbres disponibles para timbrar.");
        }

        var intento = new IntentoTimbrado
        {
            Id = Guid.NewGuid(),
            ComprobanteId = comprobanteId,
            Numero = await baseDeDatos.IntentosTimbrado.CountAsync(i => i.ComprobanteId == comprobanteId, ct) + 1,
            IniciadoUtc = DateTime.UtcNow,
            Resultado = ResultadosDeIntento.EnVuelo,
            // Una clave nueva por intento de timbrado, no por comprobante: si el PAC rechazó
            // y el usuario corrigió, es otro documento y merece otra clave. Dentro de este
            // intento se reutiliza en cada reenvío y en la conciliación.
            ClaveIdempotencia = Guid.NewGuid().ToString("N")
        };

        baseDeDatos.IntentosTimbrado.Add(intento);

        comprobante.Estatus = EstatusComprobante.Timbrando.ACadena();
        comprobante.ModificadoUtc = DateTime.UtcNow;

        await baseDeDatos.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        registro.LogInformation(
            "Comprobante {Comprobante} apartado para timbrar. Intento {Numero}, timbres restantes {Restantes}.",
            comprobanteId, intento.Numero, timbre.DisponiblesDespues);

        return (comprobante, intento);
    }

    // ── Paso 2 ──────────────────────────────────────────────────────────────────────────

    // El PAC entra por parámetro y no por el campo: TimbrarAsync ya comprobó que existe, y
    // pasarlo deja esa garantía a la vista en vez de confiarla a un comentario.
    private async Task<RespuestaDePac> EnviarConReintentosAsync(
        IProveedorPac proveedor, string documento, string clave, CancellationToken ct)
    {
        RespuestaDePac respuesta = new(ResultadoDePac.ErrorDeComunicacion, Mensaje: "No se intentó.");

        for (var intento = 0; intento <= Esperas.Length; intento++)
        {
            if (intento > 0) await Task.Delay(Esperas[intento - 1], ct);

            try
            {
                respuesta = await proveedor.TimbrarAsync(documento, clave, ct);
            }
            catch (Exception ex)
            {
                registro.LogWarning(ex, "Error de comunicación con el PAC, envío {Numero}.", intento + 1);
                respuesta = new RespuestaDePac(ResultadoDePac.ErrorDeComunicacion, Mensaje: ex.Message);
            }

            // Solo se reintenta lo que puede cambiar de resultado. Un rechazo es del
            // comprobante: insistir da el mismo rechazo y gasta tiempo del usuario.
            if (respuesta.Resultado != ResultadoDePac.ErrorDeComunicacion) return respuesta;
        }

        return respuesta;
    }

    // ── Paso 3 ──────────────────────────────────────────────────────────────────────────

    private async Task<Resultado<Comprobante>> ResolverAsync(
        Comprobante comprobante, IntentoTimbrado intento, RespuestaDePac respuesta, CancellationToken ct)
    {
        switch (respuesta.Resultado)
        {
            case ResultadoDePac.Timbrado:
                await cierre.ConfirmarAsync(comprobante, intento, respuesta, ct);
                return comprobante;

            case ResultadoDePac.Rechazado:
                await cierre.RevertirAsync(comprobante, intento, respuesta.CodigoError, respuesta.Mensaje, ct);
                return ErrorNegocio.Regla(
                    "pac-rechazo",
                    $"El PAC rechazó el comprobante: {respuesta.Mensaje ?? "sin detalle"}");

            default:
                // Ni se confirma ni se revierte: no sabemos si el PAC lo selló. Se queda en
                // 'timbrando' con el intento en vuelo, y la conciliación lo resuelve.
                registro.LogError(
                    "Timbrado de {Comprobante} sin resolver tras {Envios} envíos. Queda para conciliación.",
                    comprobante.Id, Esperas.Length + 1);

                return ErrorNegocio.Regla(
                    "timbrado-sin-confirmar",
                    "No se pudo confirmar el timbrado con el PAC. El comprobante quedó en proceso y " +
                    "el sistema lo resolverá automáticamente; no lo vuelvas a timbrar.");
        }
    }
}
