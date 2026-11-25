using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Pac;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Cancelacion;

/// Pantalla de solicitudes de cancelación: qué se pidió cancelar y qué dice hoy el SAT.
/// La verificación masiva reutiliza ConsultarEstatusAsync, la misma del botón por renglón.
/// Una consulta por comprobante, sin transacción y con tope por petición para no congelar la pantalla.
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

    // El botón «Verificar estatus SAT»: consulta todas las solicitudes abiertas.
    public async Task<Resultado<ResultadoDeVerificacionMasivaDto>> VerificarAbiertasAsync(CancellationToken ct)
    {
        if (pac is null)
            return ErrorNegocio.Regla("pac-no-configurado", "No hay un proveedor de timbrado configurado.");

        // El SAT responde por comprobante, no por solicitud: cada comprobante se consulta una vez.
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
                // Un comprobante que falla no detiene a los demás.
                registro.LogWarning(ex, "No se pudo verificar ante el SAT el comprobante {Comprobante}.",
                    pendiente.ComprobanteId);
                fallidas++;
            }
        }

        return new ResultadoDeVerificacionMasivaDto(
            revisadas, actualizadas, fallidas, Math.Max(0, abiertas.Count - TopePorVerificacion));
    }
}
