namespace Facturacion.Shared.Contratos;

/// <summary>
/// Bolsa de timbres de la cuenta de la empresa activa. Desde el 27 de octubre de 2026 la bolsa
/// es por cuenta (AGENTS.md §11): todas las empresas del mismo dueño gastan de ella, y cada
/// movimiento conserva la empresa que lo originó.
/// <para>
/// Las tres operaciones corren en transacciones cortas. Nunca se hace una llamada HTTP
/// dentro de ellas (ARQUITECTURA.md §5).
/// </para>
/// </summary>
public interface IServicioTimbres
{
    /// <summary>
    /// Aparta un timbre para el comprobante: baja el saldo disponible y sube el reservado.
    /// Si no hay saldo devuelve un error de negocio, no una excepción.
    /// </summary>
    Task<ReservaTimbreDto> ReservarAsync(Guid comprobanteId, CancellationToken ct);

    /// <summary>Consume la reserva: baja el reservado y registra el movimiento.</summary>
    Task ConfirmarAsync(Guid reservaId, CancellationToken ct);

    /// <summary>
    /// Devuelve la reserva al saldo disponible con su motivo. Lo llama el timbrado cuando
    /// falla, y también el barrido de reservas que llevan más de treinta minutos sin resolverse.
    /// </summary>
    Task DevolverAsync(Guid reservaId, string motivo, CancellationToken ct);

    /// <summary>Timbres disponibles ahora mismo, sin contar los reservados.</summary>
    Task<int> DisponiblesAsync(CancellationToken ct);
}

/// <summary>Timbre apartado para un comprobante.</summary>
/// <param name="ReservaId">Identificador de la reserva; se usa para confirmarla o devolverla.</param>
/// <param name="DisponiblesDespues">Saldo disponible tras apartar, para poder avisar cuando queda poco.</param>
public sealed record ReservaTimbreDto(
    Guid ReservaId,
    int DisponiblesDespues);
