namespace Facturacion.Shared.Contratos;

/// <summary>
/// Reserva de folios de una serie. La reserva ocurre dentro de un procedimiento almacenado
/// con bloqueo de renglón; nunca con <c>SELECT MAX(Folio)+1</c> (ARQUITECTURA.md §5).
/// </summary>
public interface IServicioFolios
{
    /// <summary>
    /// Aparta el siguiente folio de la serie y devuelve la reserva.
    /// El folio no se le muestra al usuario hasta que el comprobante quede timbrado.
    /// </summary>
    Task<FolioReservadoDto> ReservarAsync(Guid serieId, CancellationToken ct);

    /// <summary>
    /// Marca la reserva como usada por el comprobante indicado. Se llama cuando el timbrado
    /// termina bien.
    /// <para>
    /// Sin esta llamada no hay forma de distinguir una reserva en curso de una abandonada,
    /// y el barrido de reservas huérfanas no tendría contra qué comparar.
    /// </para>
    /// </summary>
    Task ConfirmarAsync(Guid reservaId, Guid comprobanteId, CancellationToken ct);

    /// <summary>
    /// Marca la reserva como abandonada cuando el timbrado no llegó a usarla.
    /// <para>
    /// <b>No devuelve el folio a la serie.</b> ARQUITECTURA.md §5 es explícito: si el timbrado
    /// falla después de tomar folio, el comprobante queda en <c>error</c> con ese folio
    /// apartado y el folio no se recicla. Este método solo deja el registro para la
    /// bitácora y para poder explicar después el hueco en la numeración.
    /// </para>
    /// </summary>
    Task LiberarSiNoUsadoAsync(Guid reservaId, CancellationToken ct);
}

/// <summary>Folio apartado para un comprobante todavía no timbrado.</summary>
/// <param name="ReservaId">Identificador de la reserva; se usa para confirmarla o abandonarla.</param>
/// <param name="SerieId">Serie de la que salió.</param>
/// <param name="Serie">Prefijo de la serie tal como va en el comprobante.</param>
/// <param name="Folio">Número apartado.</param>
public sealed record FolioReservadoDto(
    Guid ReservaId,
    Guid SerieId,
    string Serie,
    int Folio);
