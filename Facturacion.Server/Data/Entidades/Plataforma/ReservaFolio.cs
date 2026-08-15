namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Un folio apartado de una serie. Existe para poder explicar después cada número de la
/// numeración, incluidos los que no llegaron a ningún comprobante.
///
/// <para><b>Un folio abandonado no se recicla</b></para>
/// CLAUDE.md §5 es explícito: si el timbrado falla después de tomar folio, el comprobante
/// queda en error con ese folio apartado y el folio no vuelve a la serie. Reciclarlo
/// significaría que dos comprobantes distintos pudieran llevar el mismo número en momentos
/// distintos, que es exactamente lo que la numeración consecutiva sirve para impedir.
/// Por eso <see cref="EstadosDeReservaFolio.Abandonado"/> deja registro en vez de devolver
/// el número: el hueco en la numeración queda justificado y auditable.
/// </para>
/// </summary>
public sealed class ReservaFolio : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid SerieId { get; set; }

    public Serie Serie { get; set; } = null!;

    public int Folio { get; set; }

    /// <summary>Uno de <see cref="EstadosDeReservaFolio"/>.</summary>
    public required string Estado { get; set; }

    /// <summary>
    /// Comprobante que acabó usando el folio. Lo llena la mitad B al confirmar; nulo
    /// mientras la reserva sigue en curso o si se abandonó.
    /// </summary>
    public Guid? ComprobanteId { get; set; }

    public DateTime MomentoUtc { get; set; }

    /// <summary>Cuándo se confirmó o se abandonó. Nulo mientras sigue reservado.</summary>
    public DateTime? MomentoResolucionUtc { get; set; }
}

/// <summary>Estados de una reserva de folio. Cadenas fijas para poder filtrarlas sin adivinar.</summary>
public static class EstadosDeReservaFolio
{
    /// <summary>Apartado, todavía sin comprobante. Es el estado en el que nace.</summary>
    public const string Reservado = "reservado";

    /// <summary>El comprobante se timbró con este folio.</summary>
    public const string Confirmado = "confirmado";

    /// <summary>El timbrado no llegó a usarlo. El folio <b>no</b> vuelve a la serie.</summary>
    public const string Abandonado = "abandonado";
}
