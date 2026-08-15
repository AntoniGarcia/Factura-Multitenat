namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Una compra de un paquete de timbres. Es de la empresa que la hizo, porque la bolsa que va
/// a recibir los timbres es por empresa.
///
/// <para><b>Los datos del paquete se copian, no se referencian</b></para>
/// <see cref="PaqueteId"/> queda para poder rastrear, pero el nombre, la cantidad y los
/// precios se copian al comprar. Es la misma regla de inmutabilidad que rige a los
/// comprobantes (CLAUDE.md §5): que el SaaS suba el precio del paquete mañana no puede
/// reescribir lo que un cliente pagó ayer.
///
/// <para><b>El saldo no sube al comprar</b></para>
/// La compra nace en <see cref="EstadosDeCompra.PendienteDePago"/> y no toca la bolsa. Los
/// timbres entran cuando se acredita el pago, y ese es el único punto donde se crea saldo.
/// </summary>
public sealed class CompraTimbres : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid PaqueteId { get; set; }

    /// <summary>Quién la solicitó.</summary>
    public Guid UsuarioId { get; set; }

    public required string NombrePaquete { get; set; }

    public int CantidadTimbres { get; set; }

    public decimal PrecioPorTimbre { get; set; }

    public decimal PrecioTotal { get; set; }

    /// <summary>
    /// Vigencia que tenía el paquete al comprarlo, copiada por la misma razón que los
    /// precios: cambiarla en el catálogo no puede acortar la de una compra ya hecha.
    /// </summary>
    public int VigenciaMeses { get; set; }

    /// <summary>Uno de <see cref="EstadosDeCompra"/>.</summary>
    public required string Estado { get; set; }

    public DateTime CreadaUtc { get; set; }

    /// <summary>Cuándo se acreditó el pago. Nulo mientras siga pendiente.</summary>
    public DateTime? AcreditadaUtc { get; set; }

    /// <summary>
    /// Hasta cuándo son utilizables los timbres de esta compra, según la vigencia que tenía
    /// el paquete al comprarlo. Se calcula al acreditar, no al solicitar: mientras la compra
    /// no está pagada no hay timbres que puedan vencerse.
    /// <para>
    /// Se registra pero todavía no se aplica; ver <see cref="Paquete.VigenciaMeses"/>.
    /// </para>
    /// </summary>
    public DateTime? VenceUtc { get; set; }
}

/// <summary>Estados de una compra. Cadenas fijas para poder filtrarlas sin adivinar.</summary>
public static class EstadosDeCompra
{
    /// <summary>Solicitada y esperando que se acredite el pago. Es el estado en el que nace.</summary>
    public const string PendienteDePago = "pendiente_de_pago";

    /// <summary>Pago acreditado y timbres ya en la bolsa.</summary>
    public const string Pagada = "pagada";

    /// <summary>Se descartó sin acreditarse. No entregó timbres.</summary>
    public const string Cancelada = "cancelada";
}
