using System.ComponentModel.DataAnnotations;

namespace Facturacion.Shared.Operador;

/// <summary>
/// Un paquete visto desde el panel del proveedor. A diferencia del que ve el inquilino, este
/// sí expone si está activo y en qué orden se muestra, que es justo lo que el operador
/// administra.
/// </summary>
/// <param name="Compras">
/// Cuántas veces se ha comprado. Sirve para decidir si retirar un paquete de la venta es
/// inocuo o toca historia que alguien podría consultar.
/// </param>
public sealed record PaqueteDeOperadorDto(
    Guid Id,
    string Nombre,
    int CantidadTimbres,
    decimal PrecioPorTimbre,
    decimal PrecioTotal,
    int VigenciaMeses,
    bool Activo,
    int Orden,
    int Compras);

/// <summary>
/// Alta o cambio de un paquete.
/// <para>
/// El precio por timbre se captura a mano, no se deriva del total: hay tarifas que no salen
/// de una división exacta —promociones, precios redondeados para el catálogo— y el operador
/// decide qué anunciar. El formulario avisa cuando los dos números no cuadran entre sí, pero
/// no lo impide.
/// </para>
/// </summary>
public sealed record PeticionGuardarPaquete(
    [property: Required, MaxLength(100)] string Nombre,
    [property: Range(1, 1_000_000)] int CantidadTimbres,
    [property: Range(0.000001, 10_000_000)] decimal PrecioPorTimbre,
    [property: Range(0.000001, 10_000_000)] decimal PrecioTotal,
    [property: Range(1, 120)] int VigenciaMeses,
    [property: Range(0, 999)] int Orden);
