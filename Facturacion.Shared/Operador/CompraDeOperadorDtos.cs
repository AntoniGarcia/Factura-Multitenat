using System.ComponentModel.DataAnnotations;

namespace Facturacion.Shared.Operador;

/// <summary>
/// Una compra vista desde el panel del proveedor. A diferencia de la que ve el inquilino,
/// esta dice <b>de quién</b> es: sin el nombre de la empresa, una lista de compras de todos
/// los clientes no se puede acreditar.
/// </summary>
public sealed record CompraDeOperadorDto(
    Guid Id,
    string EmpresaNombre,
    string EmpresaRfc,
    string CuentaNombre,
    string NombrePaquete,
    int CantidadTimbres,
    decimal PrecioPorTimbre,
    decimal PrecioTotal,
    string Estado,
    DateTime CreadaUtc,
    DateTime? AcreditadaUtc,
    DateTime? VenceUtc,
    DateTime? CanceladaUtc,
    string? MotivoCancelacion);

/// <summary>Una página de compras y cuántas hay en total con ese filtro.</summary>
public sealed record PaginaDeComprasDeOperador(
    IReadOnlyList<CompraDeOperadorDto> Elementos,
    int Total);

/// <summary>
/// Descartar una compra que no se va a cobrar. El motivo es obligatorio: es lo único que
/// explicará meses después por qué esa compra no entregó timbres.
/// </summary>
public sealed record PeticionRechazarCompra(
    [property: Required, MaxLength(300)] string Motivo);

/// <summary>
/// Asignación directa de timbres a una sola empresa por el operador. No pasa por un paquete
/// del catálogo: el operador decide cuántos timbres, su precio unitario y cuánta vigencia.
/// La compra queda pagada de inmediato y conserva esos importes en el historial.
/// </summary>
public sealed record PeticionAsignarTimbres(
    [property: Required, Range(1, 1000000)] int CantidadTimbres,
    [property: Range(0.000001, 10_000_000)] decimal PrecioPorTimbre,
    [property: Required, Range(1, 24)] int VigenciaMeses,
    [property: Required] string ContrasenaDelOperador);
