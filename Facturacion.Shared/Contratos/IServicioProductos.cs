namespace Facturacion.Shared.Contratos;

/// <summary>
/// Lo que la mitad B necesita del catálogo de productos para armar un concepto.
/// </summary>
public interface IServicioProductos
{
    /// <summary>
    /// Devuelve el producto listo para convertirse en concepto del comprobante.
    /// Devuelve <c>null</c> si no existe o no es de la empresa activa.
    /// </summary>
    Task<ProductoParaConceptoDto?> ObtenerParaConceptoAsync(Guid productoId, CancellationToken ct);
}

/// <summary>
/// Producto o servicio con todo lo que exige un concepto de CFDI 4.0.
/// </summary>
/// <param name="ProductoId">Producto del que se tomó la foto.</param>
/// <param name="ClaveProdServ">Clave de <c>c_ClaveProdServ</c>.</param>
/// <param name="ClaveUnidad">Clave de <c>c_ClaveUnidad</c>.</param>
/// <param name="UnidadTexto">Unidad en texto libre; es la que el receptor lee en el PDF.</param>
/// <param name="Descripcion">Descripción del concepto.</param>
/// <param name="ValorUnitario">Precio de venta. Seis decimales de cálculo, dos de presentación.</param>
/// <param name="ObjetoImp">Clave de <c>c_ObjetoImp</c>.</param>
/// <param name="Impuestos">Impuestos configurados en el producto. Vacío cuando <paramref name="ObjetoImp"/> indica que no es objeto de impuesto.</param>
public sealed record ProductoParaConceptoDto(
    Guid ProductoId,
    string ClaveProdServ,
    string ClaveUnidad,
    string UnidadTexto,
    string Descripcion,
    decimal ValorUnitario,
    string ObjetoImp,
    IReadOnlyList<ImpuestoProductoDto> Impuestos);

/// <summary>
/// Un impuesto configurado en el producto. Se modela con las tres columnas del SAT y no
/// como "IVA 16 / IVA 0 / exento": que la interfaz ofrezca atajos no obliga a que el
/// modelo sea pobre.
/// </summary>
/// <param name="Impuesto">Clave de <c>c_Impuesto</c>: 001 ISR, 002 IVA, 003 IEPS.</param>
/// <param name="TipoFactor">Clave de <c>c_TipoFactor</c>: Tasa, Cuota o Exento.</param>
/// <param name="TasaOCuota">Valor de <c>c_TasaOCuota</c>. Es nulo cuando el tipo de factor es Exento.</param>
/// <param name="EsRetencion">Verdadero si es retención; falso si es traslado.</param>
public sealed record ImpuestoProductoDto(
    string Impuesto,
    string TipoFactor,
    decimal? TasaOCuota,
    bool EsRetencion);
