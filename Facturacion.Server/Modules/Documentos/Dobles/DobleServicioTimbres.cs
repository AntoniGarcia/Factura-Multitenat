using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos.Dobles;

/// <summary>
/// Doble de <see cref="IServicioTimbres"/> para pruebas en Development.
/// Simula un saldo infinito de timbres, todas las operaciones son exitosas.
/// </summary>
public sealed class DobleServicioTimbres : IServicioTimbres
{
    private readonly Dictionary<Guid, string> _reservas = new();
    private readonly object _candado = new();
    private const int SaldoFicticio = 9999;

    public Task<ReservaTimbreDto> ReservarAsync(Guid comprobanteId, CancellationToken ct)
    {
        lock (_candado)
        {
            var reservaId = Guid.NewGuid();
            _reservas[reservaId] = "reservado";

            var resultado = new ReservaTimbreDto(
                ReservaId: reservaId,
                DisponiblesDespues: SaldoFicticio - 1
            );

            return Task.FromResult(resultado);
        }
    }

    public Task ConfirmarAsync(Guid reservaId, CancellationToken ct)
    {
        lock (_candado)
        {
            if (_reservas.ContainsKey(reservaId))
                _reservas[reservaId] = "confirmado";
        }

        return Task.CompletedTask;
    }

    public Task DevolverAsync(Guid reservaId, string motivo, CancellationToken ct)
    {
        lock (_candado)
        {
            if (_reservas.ContainsKey(reservaId))
                _reservas[reservaId] = "devuelto";
        }

        return Task.CompletedTask;
    }

    public Task<int> DisponiblesAsync(CancellationToken ct)
        => Task.FromResult(SaldoFicticio);
}
