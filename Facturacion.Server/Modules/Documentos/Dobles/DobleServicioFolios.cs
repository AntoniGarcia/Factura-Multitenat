using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos.Dobles;

/// <summary>
/// Doble de <see cref="IServicioFolios"/> para pruebas en Development.
/// Mantiene un contador simple que se incrementa en cada reserva.
/// </summary>
public sealed class DobleServicioFolios : IServicioFolios
{
    private int _contadorFolio = 0;
    private readonly Dictionary<Guid, int> _foliosPorReserva = new();
    private readonly Dictionary<Guid, Guid> _comprobantesPorReserva = new();
    private readonly object _candado = new();

    public Task<FolioReservadoDto> ReservarAsync(Guid serieId, CancellationToken ct)
    {
        lock (_candado)
        {
            _contadorFolio++;
            var reservaId = Guid.NewGuid();
            _foliosPorReserva[reservaId] = _contadorFolio;

            var resultado = new FolioReservadoDto(
                ReservaId: reservaId,
                SerieId: serieId,
                Serie: "TST",
                Folio: _contadorFolio
            );

            return Task.FromResult(resultado);
        }
    }

    public Task ConfirmarAsync(Guid reservaId, Guid comprobanteId, CancellationToken ct)
    {
        lock (_candado)
        {
            _comprobantesPorReserva[reservaId] = comprobanteId;
        }

        return Task.CompletedTask;
    }

    public Task LiberarSiNoUsadoAsync(Guid reservaId, CancellationToken ct)
    {
        lock (_candado)
        {
            _foliosPorReserva.Remove(reservaId);
            _comprobantesPorReserva.Remove(reservaId);
        }

        return Task.CompletedTask;
    }
}
