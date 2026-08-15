namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Cada cambio del saldo de timbres de una empresa, con su motivo, su usuario y su momento.
/// Es el libro contable de la bolsa: <see cref="BolsaTimbres"/> es su suma, no al revés.
///
/// <para><b>Dos deltas, no una cantidad</b></para>
/// Un movimiento puede mover los dos contadores a la vez y en sentidos opuestos —reservar
/// baja uno y sube el otro—, así que una sola columna de cantidad no alcanzaría para
/// reconstruir el saldo. Con dos deltas con signo, la suma de cada columna reproduce
/// exactamente el contador que le corresponde, y ese es el chequeo de integridad de toda
/// la bolsa.
///
/// <para><b>Nada se corrige borrando</b></para>
/// Un movimiento equivocado se compensa con otro de <see cref="TiposDeMovimientoTimbre.Ajuste"/>
/// y su motivo. Borrar un renglón dejaría el saldo sin explicación.
/// </summary>
public sealed class MovimientoTimbre : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    /// <summary>Uno de <see cref="TiposDeMovimientoTimbre"/>.</summary>
    public required string Tipo { get; set; }

    /// <summary>Cuánto cambió el saldo disponible. Negativo cuando baja.</summary>
    public int DeltaDisponible { get; set; }

    /// <summary>Cuánto cambió el saldo reservado. Negativo cuando baja.</summary>
    public int DeltaReservado { get; set; }

    /// <summary>Saldo disponible tras aplicar este movimiento, para poder auditar sin sumar.</summary>
    public int DisponiblesDespues { get; set; }

    public int ReservadosDespues { get; set; }

    /// <summary>
    /// Por qué ocurrió. Obligatorio en devoluciones y ajustes, que son los que hay que poder
    /// explicar meses después.
    /// </summary>
    public string? Motivo { get; set; }

    /// <summary>
    /// Quién lo provocó. Nulo cuando lo genera el sistema sin usuario delante: el barrido de
    /// reservas abandonadas y la acreditación por consola.
    /// </summary>
    public Guid? UsuarioId { get; set; }

    public Guid? CompraId { get; set; }

    public Guid? ReservaId { get; set; }

    public DateTime MomentoUtc { get; set; }
}

/// <summary>Tipos de movimiento. Cadenas fijas para poder filtrarlas sin adivinar.</summary>
public static class TiposDeMovimientoTimbre
{
    /// <summary>Entran timbres porque se acreditó el pago de una compra.</summary>
    public const string Compra = "compra";

    /// <summary>Se aparta un timbre para un comprobante: baja disponible, sube reservado.</summary>
    public const string Reserva = "reserva";

    /// <summary>El comprobante se timbró: baja reservado y el timbre se gasta.</summary>
    public const string Consumo = "consumo";

    /// <summary>El timbrado no llegó a usarlo: baja reservado, sube disponible.</summary>
    public const string Devolucion = "devolucion";

    /// <summary>Corrección manual con motivo. Es el único que puede mover el saldo sin una operación detrás.</summary>
    public const string Ajuste = "ajuste";
}
