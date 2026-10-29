namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>Oferta general si CuentaId es nulo; exclusiva de una cuenta si tiene valor. El servidor obtiene el precio a partir del identificador del paquete.</summary>
public sealed class Paquete
{
    public Guid Id { get; set; }

    public Guid? CuentaId { get; set; }

    public Cuenta? Cuenta { get; set; }

    public required string Nombre { get; set; }

    public int CantidadTimbres { get; set; }

    /// <summary>
    /// Precio unitario. Es dato derivado de <see cref="PrecioTotal"/> entre
    /// <see cref="CantidadTimbres"/>, pero se guarda porque es lo que se le anuncia al
    /// comprador y no debe recalcularse en cada pantalla con un redondeo distinto.
    /// </summary>
    public decimal PrecioPorTimbre { get; set; }

    public decimal PrecioTotal { get; set; }

    /// <summary>
    /// Meses que los timbres siguen siendo utilizables desde que se acredita la compra.
    /// <para>
    /// Hoy se <b>registra</b> pero no se aplica: caducar saldo exige llevar los timbres por
    /// lotes con su fecha y consumirlos por orden de vencimiento, y la bolsa de esta fase es
    /// un par de contadores que no puede representar eso. Se guarda para que la compra deje
    /// constancia de la vigencia que se vendió, y para no tener que rehacer el esquema
    /// cuando se decida aplicarla.
    /// </para>
    /// </summary>
    public int VigenciaMeses { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>Posición en la que se muestra en la página de compra, de menor a mayor.</summary>
    public int Orden { get; set; }
}
