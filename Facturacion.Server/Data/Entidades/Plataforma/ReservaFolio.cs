namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>Reserva un número de serie sin reutilizarlo, aunque falle el timbrado. La reserva explica los huecos de numeración.</summary>
public sealed class ReservaFolio : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid SerieId { get; set; }

    public Serie Serie { get; set; } = null!;

    public int Folio { get; set; }

    /// <summary>Uno de <see cref="EstadosDeReservaFolio"/>.</summary>
    public required string Estado { get; set; }

    /// <summary>Comprobante asociado al confirmar la reserva; nulo mientras está en curso o si se abandona.</summary>
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
