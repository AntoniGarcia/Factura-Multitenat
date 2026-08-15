namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Saldo de timbres de una empresa. Hay exactamente un renglón por empresa.
///
/// <para><b>Por qué existe si el saldo es la suma de los movimientos</b></para>
/// La verdad contable son los <see cref="MovimientoTimbre"/>: este renglón es esa suma ya
/// hecha. Existe por dos razones que no se pueden resolver sumando en cada consulta.
/// <list type="number">
///   <item>
///     Reservar el último timbre desde dos peticiones simultáneas exige un candado sobre
///     <b>algo</b>. Un <c>SUM</c> no se puede bloquear; un renglón sí. Los procedimientos
///     toman <c>UPDLOCK</c> sobre este renglón y por eso dos hilos no pueden dejar el saldo
///     en negativo.
///   </item>
///   <item>
///     Sumar cientos de miles de movimientos para pintar la tarjeta del saldo en cada carga
///     de pantalla es un costo que crece con la antigüedad del cliente.
///   </item>
/// </list>
/// <b>Nada edita estos números directamente.</b> Solo se mueven dentro del mismo
/// procedimiento que inserta el movimiento que los explica, en la misma transacción. La
/// invariante <c>SUM(DeltaDisponible) = Disponibles</c> y <c>SUM(DeltaReservado) =
/// Reservados</c> se puede comprobar en cualquier momento, y las pruebas la comprueban.
/// </summary>
public sealed class BolsaTimbres : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Empresa Empresa { get; set; } = null!;

    /// <summary>Timbres utilizables ahora mismo. No incluye los apartados.</summary>
    public int Disponibles { get; set; }

    /// <summary>
    /// Timbres apartados por un timbrado en curso. Ya no se pueden usar para otro
    /// comprobante, pero tampoco se han consumido: si el timbrado falla vuelven a
    /// <see cref="Disponibles"/>.
    /// </summary>
    public int Reservados { get; set; }

    public DateTime ActualizadaUtc { get; set; }
}
