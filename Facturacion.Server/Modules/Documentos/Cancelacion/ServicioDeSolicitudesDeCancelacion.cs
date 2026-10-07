using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Pac;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Cancelacion;

/// <summary>
/// La pantalla «Solicitudes de cancelación» de §30: qué se pidió cancelar, en qué quedó y qué
/// dice el SAT hoy. Sin ella no hay forma de saber qué solicitudes siguen esperando al receptor.
///
/// <para><b>La verificación masiva reutiliza la individual</b></para>
/// Cada comprobante se consulta con <see cref="ServicioDeCancelacion.ConsultarEstatusAsync"/>,
/// la misma que usa el botón por renglón: una sola regla para decidir cuándo el SAT dio por
/// cancelado o por rechazado algo, en vez de dos que se separen con el tiempo.
///
/// <para><b>Una llamada al SAT por comprobante, sin transacción</b></para>
/// Cada consulta guarda en su propia escritura corta (ARQUITECTURA.md §5), y hay un tope por
/// petición: con el SAT lento, cincuenta consultas seguidas dejarían al usuario viendo una
/// pantalla congelada por minutos. Lo que no entra se revisa en la siguiente pasada, empezando
/// por lo que lleva más tiempo sin consultarse.
/// </para>
/// </summary>
public sealed class ServicioDeSolicitudesDeCancelacion(
    AppDbContext baseDeDatos,
    ServicioDeCancelacion cancelacion,
    ILogger<ServicioDeSolicitudesDeCancelacion> registro,
    IProveedorPac? pac = null)
{
    private const int TopePorVerificacion = 25;

    private static readonly string[] EstadosAbiertos =
        [EstadosDeSolicitud.EnProceso, EstadosDeSolicitud.EnEsperaDelReceptor];

    private static readonly string[] EstadosConocidos =
    [
        EstadosDeSolicitud.EnProceso, EstadosDeSolicitud.EnEsperaDelReceptor, EstadosDeSolicitud.Cancelada,
        EstadosDeSolicitud.Rechazada, EstadosDeSolicitud.Error
    ];

    public async Task<Resultado<PaginaDeSolicitudesDeCancelacion>> ListarAsync(
        string? filtro, string? busca, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = baseDeDatos.SolicitudesCancelacion.AsNoTracking();

        switch (filtro)
        {
            case null or "" or FiltrosDeSolicitudes.Abiertas:
                consulta = consulta.Where(s => EstadosAbiertos.Contains(s.Estado));
                break;

            case FiltrosDeSolicitudes.Todas:
                break;

            case var estado when EstadosConocidos.Contains(estado):
                consulta = consulta.Where(s => s.Estado == estado);
                break;

            default:
                return ErrorNegocio.Validacion("filtro-desconocido", $"«{filtro}» no es un filtro de solicitudes.");
        }

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var texto = busca.Trim();
            var folio = int.TryParse(texto, out var n) ? n : (int?)null;

            consulta = consulta.Where(s =>
                s.Comprobante.ReceptorRfc.Contains(texto) ||
                s.Comprobante.ReceptorNombre.Contains(texto) ||
                (folio != null && s.Comprobante.Folio == folio));
        }

        var total = await consulta.CountAsync(ct);

        var abiertas = await baseDeDatos.SolicitudesCancelacion
            .CountAsync(s => EstadosAbiertos.Contains(s.Estado), ct);

        var elementos = await consulta
            .OrderByDescending(s => s.SolicitadaUtc)
            .Skip(pagina * tamano)
            .Take(tamano)
            .Select(s => new SolicitudDeCancelacionEnListaDto(
                s.Id,
                s.ComprobanteId,
                s.Comprobante.TipoDeComprobante,
                s.Comprobante.Serie,
                s.Comprobante.Folio,
                s.Comprobante.Uuid,
                s.Comprobante.ReceptorRfc,
                s.Comprobante.ReceptorNombre,
                s.Comprobante.Total,
                s.Comprobante.Moneda,
                s.Motivo,
                s.UuidSustituye,
                s.SolicitadaUtc,
                s.Estado,
                s.ResueltaUtc,
                s.MensajeRespuesta,
                s.Comprobante.Estatus,
                s.EstadoCfdiSat,
                s.EsCancelableSat,
                s.EstatusCancelacionSat,
                s.ConsultadaUtc))
            .ToListAsync(ct);

        return new PaginaDeSolicitudesDeCancelacion(elementos, total, abiertas);
    }

    /// <summary>El botón «Verificar estatus SAT» de §30: consulta todas las solicitudes abiertas.</summary>
    public async Task<Resultado<ResultadoDeVerificacionMasivaDto>> VerificarAbiertasAsync(CancellationToken ct)
    {
        if (pac is null)
            return ErrorNegocio.Regla("pac-no-configurado", "No hay un proveedor de timbrado configurado.");

        // Un comprobante con dos solicitudes abiertas se consulta una sola vez: el SAT responde
        // por el comprobante, no por la solicitud.
        var abiertas = (await baseDeDatos.SolicitudesCancelacion
                .AsNoTracking()
                .Where(s => EstadosAbiertos.Contains(s.Estado))
                .OrderBy(s => s.ConsultadaUtc ?? DateTime.MinValue)
                .Select(s => new { s.ComprobanteId, s.Comprobante.Estatus })
                .ToListAsync(ct))
            .DistinctBy(s => s.ComprobanteId)
            .ToList();

        var revisadas = 0;
        var actualizadas = 0;
        var fallidas = 0;

        foreach (var pendiente in abiertas.Take(TopePorVerificacion))
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var resultado = await cancelacion.ConsultarEstatusAsync(pendiente.ComprobanteId, ct);

                if (resultado.EsFallo)
                {
                    fallidas++;
                    continue;
                }

                revisadas++;

                if (resultado.Valor.EstatusLocal != pendiente.Estatus) actualizadas++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Un comprobante que falla no detiene a los demás: se registra y se sigue.
                registro.LogWarning(ex, "No se pudo verificar ante el SAT el comprobante {Comprobante}.",
                    pendiente.ComprobanteId);
                fallidas++;
            }
        }

        return new ResultadoDeVerificacionMasivaDto(
            revisadas, actualizadas, fallidas, Math.Max(0, abiertas.Count - TopePorVerificacion));
    }
}
