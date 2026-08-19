using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Documentos.Pac;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Documentos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Cancelacion;

/// <summary>
/// Cancelación de comprobantes ante el SAT (B8) y consulta de su estado (§30 del documento
/// funcional).
///
/// <para><b>Mismos tres pasos que el timbrado, y por la misma razón</b></para>
/// La llamada al SAT tarda y no puede ocurrir dentro de una transacción (CLAUDE.md §5):
/// apartar en transacción corta, hablar con el PAC fuera de toda transacción, resolver en otra
/// transacción corta. Ver <c>ServicioDeTimbrado</c>, que documenta el porqué a fondo.
///
/// <para><b>Cancelar no devuelve el timbre</b></para>
/// Es una decisión, no un olvido. El timbre se gastó al timbrar y el PAC no lo reembolsa;
/// además <see cref="IServicioTimbres"/> —congelado— solo sabe devolver <i>reservas</i> vivas,
/// y la de un comprobante timbrado ya se consumió. Acreditar un timbre aquí sería inventar
/// saldo que nadie pagó.
///
/// <para><b>La cancelación no siempre es inmediata</b></para>
/// Con los motivos que exigen aceptación del receptor, el SAT solo registra la solicitud: el
/// comprobante <b>sigue siendo fiscalmente válido</b> y queda en <c>en_cancelacion</c> hasta
/// que el receptor acepte, rechace o se venzan los tres días hábiles. Darlo por cancelado en
/// ese momento haría que el sistema y el SAT discreparan justo en el dato que importa.
/// </summary>
public sealed class ServicioDeCancelacion(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno contexto,
    IProveedorCsdParaTimbrado csd,
    ILogger<ServicioDeCancelacion> registro,
    IProveedorPac? pac = null)
{
    private static readonly string[] MotivosValidos =
    [
        MotivosDeCancelacion.ConErroresConRelacion,
        MotivosDeCancelacion.ConErroresSinRelacion,
        MotivosDeCancelacion.NoSeLlevoACabo,
        MotivosDeCancelacion.NominativaEnGlobal
    ];

    public async Task<Resultado<ResultadoDeCancelacionDto>> CancelarAsync(
        Guid comprobanteId, PeticionDeCancelacion peticion, CancellationToken ct)
    {
        // Antes de tocar nada: sin PAC no hay cancelación posible, y marcar el comprobante
        // como 'en_cancelacion' para luego no poder mandarla lo dejaría atorado ahí.
        if (pac is null)
        {
            registro.LogError("Se intentó cancelar sin un PAC registrado.");

            return ErrorNegocio.Regla(
                "pac-no-configurado",
                "No hay un proveedor de timbrado configurado. El comprobante no se tocó.");
        }

        var apartado = await ApartarAsync(comprobanteId, peticion, ct);

        if (apartado.EsFallo) return apartado.Error!;

        var (comprobante, solicitud) = apartado.Valor;

        // ── Paso 2: fuera de toda transacción ───────────────────────────────────────────
        RespuestaDeCancelacion respuesta;

        try
        {
            var certificado = await csd.ObtenerAsync(ct);

            respuesta = await pac.CancelarAsync(
                new DatosDeCancelacion(
                    comprobante.Uuid!.Value,
                    comprobante.EmisorRfc,
                    solicitud.Motivo,
                    solicitud.UuidSustituye,
                    certificado.CertificadoCer,
                    certificado.LlavePrivadaKey,
                    certificado.ContrasenaLlave),
                ct);
        }
        catch (Exception ex)
        {
            // Una excepción antes de enviar es nuestra, no del SAT: la solicitud nunca salió,
            // así que se puede revertir sin riesgo de dejar el sistema y el SAT en desacuerdo.
            registro.LogError(ex, "Falló la preparación de la cancelación de {Comprobante}.", comprobanteId);

            await RevertirAsync(comprobante, solicitud, "error-al-preparar", ct);

            return ErrorNegocio.Regla(
                "error-al-preparar",
                "No se pudo preparar la cancelación. El comprobante sigue vigente.");
        }

        return await ResolverAsync(comprobante, solicitud, respuesta, ct);
    }

    // ── Paso 1 ──────────────────────────────────────────────────────────────────────────

    private async Task<Resultado<(Comprobante, SolicitudCancelacion)>> ApartarAsync(
        Guid comprobanteId, PeticionDeCancelacion peticion, CancellationToken ct)
    {
        var comprobante = await baseDeDatos.Comprobantes
            .FirstOrDefaultAsync(c => c.Id == comprobanteId, ct);

        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("comprobante-no-encontrado", "Ese comprobante no existe.");

        if (comprobante.Estatus == EstatusComprobante.EnCancelacion.ACadena())
            return ErrorNegocio.Conflicto(
                "cancelacion-en-curso",
                "Ya hay una cancelación en curso para este comprobante. Verifica su estatus ante el SAT.");

        if (comprobante.Estatus == EstatusComprobante.Cancelado.ACadena())
            return ErrorNegocio.Conflicto("comprobante-ya-cancelado", "Ese comprobante ya está cancelado.");

        // Solo lo timbrado se cancela. Un borrador se descarta y un comprobante en error nunca
        // llegó al SAT: no hay nada que cancelar ante nadie.
        if (comprobante.Estatus != EstatusComprobante.Timbrado.ACadena() || comprobante.Uuid is null)
            return ErrorNegocio.Conflicto(
                "comprobante-no-cancelable",
                $"El comprobante está en '{comprobante.Estatus}' y solo se puede cancelar uno timbrado.");

        var validado = await ValidarMotivoAsync(comprobante, peticion, ct);

        if (validado.EsFallo) return validado.Error!;

        var solicitud = new SolicitudCancelacion
        {
            Id = Guid.NewGuid(),
            ComprobanteId = comprobante.Id,
            Motivo = peticion.Motivo,
            UuidSustituye = validado.Valor,
            Estado = EstadosDeSolicitud.EnProceso,
            SolicitadaUtc = DateTime.UtcNow,
            SolicitadaPorUsuarioId = contexto.UsuarioActual ?? Guid.Empty
        };

        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(ct);

        baseDeDatos.SolicitudesCancelacion.Add(solicitud);

        comprobante.Estatus = EstatusComprobante.EnCancelacion.ACadena();
        comprobante.ModificadoUtc = DateTime.UtcNow;

        await baseDeDatos.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        registro.LogInformation(
            "Cancelación solicitada para {Comprobante} con motivo {Motivo}.", comprobante.Id, peticion.Motivo);

        return (comprobante, solicitud);
    }

    /// <summary>
    /// Comprueba el motivo y resuelve el UUID sustituto. El SAT <b>exige</b> ese folio con el
    /// motivo 01 y <b>rechaza</b> la solicitud si viene con cualquier otro, así que la regla se
    /// aplica en los dos sentidos y no solo en uno.
    /// </summary>
    private async Task<Resultado<Guid?>> ValidarMotivoAsync(
        Comprobante comprobante, PeticionDeCancelacion peticion, CancellationToken ct)
    {
        if (!MotivosValidos.Contains(peticion.Motivo))
            return ErrorNegocio.Validacion(
                "motivo-desconocido",
                $"«{peticion.Motivo}» no es un motivo de cancelación del SAT. Son 01, 02, 03 y 04.");

        if (peticion.Motivo != MotivosDeCancelacion.ConErroresConRelacion)
        {
            if (peticion.UuidSustituye is not null)
                return ErrorNegocio.Validacion(
                    "sustituto-no-aplica",
                    "El folio del comprobante que sustituye solo se manda con el motivo 01.");

            return Resultado<Guid?>.Exito(null);
        }

        if (peticion.UuidSustituye is not { } sustituye)
            return ErrorNegocio.Validacion(
                "sustituto-requerido",
                "El motivo 01 exige el folio fiscal del comprobante que sustituye a este.");

        if (sustituye == comprobante.Uuid)
            return ErrorNegocio.Validacion(
                "sustituto-es-el-mismo",
                "Un comprobante no puede sustituirse a sí mismo.");

        // Se exige que el sustituto exista y esté timbrado en esta empresa. El SAT lo validaría
        // de todos modos, pero enterarse aquí evita dejar el comprobante en 'en_cancelacion'
        // esperando un rechazo que ya se podía prever.
        var existe = await baseDeDatos.Comprobantes.AnyAsync(
            c => c.Uuid == sustituye && c.Estatus == EstatusComprobante.Timbrado.ACadena(), ct);

        if (!existe)
            return ErrorNegocio.Validacion(
                "sustituto-no-encontrado",
                $"No hay un comprobante timbrado de esta empresa con el folio fiscal {sustituye}.");

        return Resultado<Guid?>.Exito(sustituye);
    }

    // ── Paso 3 ──────────────────────────────────────────────────────────────────────────

    private async Task<Resultado<ResultadoDeCancelacionDto>> ResolverAsync(
        Comprobante comprobante, SolicitudCancelacion solicitud, RespuestaDeCancelacion respuesta,
        CancellationToken ct)
    {
        solicitud.CodigoRespuesta = respuesta.CodigoRespuesta;
        solicitud.MensajeRespuesta = respuesta.Mensaje;

        switch (respuesta.Resultado)
        {
            case ResultadoDeCancelacion.Cancelado:
                comprobante.Estatus = EstatusComprobante.Cancelado.ACadena();
                solicitud.Estado = EstadosDeSolicitud.Cancelada;
                solicitud.ResueltaUtc = DateTime.UtcNow;
                break;

            case ResultadoDeCancelacion.EnEsperaDelReceptor:
                // El comprobante se queda en 'en_cancelacion' a propósito: hasta que el
                // receptor conteste sigue siendo válido ante el SAT.
                solicitud.Estado = EstadosDeSolicitud.EnEsperaDelReceptor;
                break;

            case ResultadoDeCancelacion.Rechazado:
                comprobante.Estatus = EstatusComprobante.Timbrado.ACadena();
                solicitud.Estado = EstadosDeSolicitud.Rechazada;
                solicitud.ResueltaUtc = DateTime.UtcNow;
                break;

            default:
                // Ni se confirma ni se revierte: no se sabe si el SAT la registró. Se queda en
                // 'en_cancelacion' y de ahí lo saca la consulta de estatus de §30.
                solicitud.Estado = EstadosDeSolicitud.EnProceso;

                registro.LogError(
                    "Cancelación de {Comprobante} sin resolver. Queda para la consulta de estatus.",
                    comprobante.Id);
                break;
        }

        comprobante.ModificadoUtc = DateTime.UtcNow;
        await baseDeDatos.SaveChangesAsync(ct);

        if (respuesta.Resultado == ResultadoDeCancelacion.Rechazado)
            return ErrorNegocio.Regla(
                "cancelacion-rechazada",
                respuesta.Mensaje ?? "El SAT rechazó la cancelación. El comprobante sigue vigente.");

        if (respuesta.Resultado == ResultadoDeCancelacion.ErrorDeComunicacion)
            return ErrorNegocio.Regla(
                "cancelacion-sin-confirmar",
                "No se pudo confirmar la cancelación con el SAT. El comprobante quedó en proceso; " +
                "usa «Verificar estatus SAT» en vez de volver a cancelarlo.");

        return ADto(comprobante, solicitud);
    }

    private async Task RevertirAsync(
        Comprobante comprobante, SolicitudCancelacion solicitud, string motivo, CancellationToken ct)
    {
        comprobante.Estatus = EstatusComprobante.Timbrado.ACadena();
        comprobante.ModificadoUtc = DateTime.UtcNow;

        solicitud.Estado = EstadosDeSolicitud.Error;
        solicitud.CodigoRespuesta = motivo;
        solicitud.ResueltaUtc = DateTime.UtcNow;

        await baseDeDatos.SaveChangesAsync(ct);
    }

    // ── Consulta de estatus (§30) ───────────────────────────────────────────────────────

    /// <summary>
    /// Pregunta al SAT cómo está el comprobante y ajusta el sistema a lo que conteste. El SAT
    /// es la autoridad: si dice que está cancelado, lo está, aunque aquí figure de otro modo.
    /// </summary>
    public async Task<Resultado<EstatusSatDto>> ConsultarEstatusAsync(Guid comprobanteId, CancellationToken ct)
    {
        if (pac is null)
            return ErrorNegocio.Regla(
                "pac-no-configurado", "No hay un proveedor de timbrado configurado.");

        var comprobante = await baseDeDatos.Comprobantes
            .FirstOrDefaultAsync(c => c.Id == comprobanteId, ct);

        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("comprobante-no-encontrado", "Ese comprobante no existe.");

        if (comprobante.Uuid is not { } uuid)
            return ErrorNegocio.Conflicto(
                "comprobante-sin-timbre",
                "Ese comprobante nunca se timbró, así que el SAT no sabe nada de él.");

        var estatus = await pac.ConsultarEstatusAsync(
            new DatosDeConsultaSat(uuid, comprobante.EmisorRfc, comprobante.ReceptorRfc, comprobante.Total), ct);

        if (!estatus.Consultado)
            return ErrorNegocio.Regla(
                "consulta-sat-fallida",
                estatus.Mensaje ?? "No se pudo consultar el estatus ante el SAT.");

        await SincronizarAsync(comprobante, estatus, ct);

        return new EstatusSatDto(
            comprobante.Id,
            uuid,
            estatus.EstadoCfdi,
            estatus.EsCancelable,
            estatus.EstatusCancelacion,
            estatus.CodigoEstatus,
            comprobante.Estatus);
    }

    /// <summary>
    /// Alinea el estatus local con lo que dijo el SAT. Solo se mueve en las direcciones que
    /// tienen sentido: nada de «resucitar» un comprobante que aquí ya figura cancelado por una
    /// respuesta ambigua.
    /// </summary>
    private async Task SincronizarAsync(Comprobante comprobante, EstatusSatDePac estatus, CancellationToken ct)
    {
        var estado = (estatus.EstadoCfdi ?? string.Empty).ToUpperInvariant();
        var cancelacion = (estatus.EstatusCancelacion ?? string.Empty).ToUpperInvariant();
        var antes = comprobante.Estatus;

        var solicitud = await baseDeDatos.SolicitudesCancelacion
            .Where(s => s.ComprobanteId == comprobante.Id)
            .OrderByDescending(s => s.SolicitadaUtc)
            .FirstOrDefaultAsync(ct);

        if (estado.Contains("CANCELADO", StringComparison.Ordinal))
        {
            comprobante.Estatus = EstatusComprobante.Cancelado.ACadena();

            if (solicitud is { ResueltaUtc: null })
            {
                solicitud.Estado = EstadosDeSolicitud.Cancelada;
                solicitud.ResueltaUtc = DateTime.UtcNow;
            }
        }
        else if (estado.Contains("VIGENTE", StringComparison.Ordinal))
        {
            // Vigente y con solicitud rechazada o vencida: la cancelación no prosperó y el
            // comprobante vuelve a ser una factura normal.
            var noProspero =
                cancelacion.Contains("RECHAZAD", StringComparison.Ordinal) ||
                cancelacion.Contains("PLAZO VENCIDO", StringComparison.Ordinal) ||
                cancelacion.Length == 0;

            if (comprobante.Estatus == EstatusComprobante.EnCancelacion.ACadena() && noProspero)
            {
                comprobante.Estatus = EstatusComprobante.Timbrado.ACadena();

                if (solicitud is { ResueltaUtc: null })
                {
                    solicitud.Estado = EstadosDeSolicitud.Rechazada;
                    solicitud.ResueltaUtc = DateTime.UtcNow;
                }
            }
        }

        if (comprobante.Estatus == antes) return;

        comprobante.ModificadoUtc = DateTime.UtcNow;
        await baseDeDatos.SaveChangesAsync(ct);

        registro.LogInformation(
            "El SAT movió a {Comprobante} de '{Antes}' a '{Despues}'.", comprobante.Id, antes, comprobante.Estatus);
    }

    private static ResultadoDeCancelacionDto ADto(Comprobante comprobante, SolicitudCancelacion solicitud)
        => new(
            comprobante.Id,
            comprobante.Estatus,
            solicitud.Motivo,
            solicitud.UuidSustituye,
            solicitud.Estado,
            solicitud.SolicitadaUtc,
            solicitud.ResueltaUtc,
            solicitud.MensajeRespuesta);
}
