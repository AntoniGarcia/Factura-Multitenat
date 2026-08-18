namespace Facturacion.Server.Modules.Documentos.Impuestos;

// Entrada y salida del motor de impuestos. Son registros inmutables y no entidades de EF a
// propósito: el motor tiene que poder ejercitarse sin base de datos, que es lo que permite
// cubrirlo con decenas de casos en milisegundos.

/// <summary>Un impuesto configurado en un renglón, antes de calcularse.</summary>
/// <param name="Impuesto">Clave de <c>c_Impuesto</c>: 001 ISR, 002 IVA, 003 IEPS.</param>
/// <param name="TipoFactor">Clave de <c>c_TipoFactor</c>: Tasa, Cuota o Exento.</param>
/// <param name="TasaOCuota">Nula —y solo puede serlo— cuando el tipo de factor es Exento.</param>
public sealed record ImpuestoDeConcepto(
    string Impuesto,
    string TipoFactor,
    decimal? TasaOCuota,
    bool EsRetencion);

/// <summary>Un renglón por calcular.</summary>
/// <param name="ObjetoImp">
/// Clave de <c>c_ObjetoImp</c>: 01 no objeto, 02 sí objeto, 03 sí objeto y no obligado al
/// desglose.
/// </param>
public sealed record ConceptoACalcular(
    decimal Cantidad,
    decimal ValorUnitario,
    decimal Descuento,
    string ObjetoImp,
    IReadOnlyList<ImpuestoDeConcepto> Impuestos);

/// <summary>El comprobante completo por calcular.</summary>
/// <param name="TipoCambio">
/// Obligatorio cuando la moneda no es MXN. Solo se declara: los importes ya van en la moneda
/// del comprobante y no se convierten.
/// </param>
public sealed record ComprobanteACalcular(
    string Moneda,
    decimal? TipoCambio,
    IReadOnlyList<ConceptoACalcular> Conceptos);

/// <summary>Un impuesto ya calculado sobre su renglón.</summary>
/// <param name="Importe">
/// Nulo cuando el tipo de factor es Exento. No es lo mismo que cero: un exento no lleva
/// importe en el XML y una tasa cero sí lo lleva, valiendo cero.
/// </param>
public sealed record ImpuestoCalculado(
    string Impuesto,
    string TipoFactor,
    decimal? TasaOCuota,
    decimal Base,
    decimal? Importe,
    bool EsRetencion);

/// <summary>Un renglón ya calculado.</summary>
/// <param name="Base">Importe menos descuento. Es sobre esto que se aplican las tasas.</param>
public sealed record ConceptoCalculado(
    decimal Importe,
    decimal Descuento,
    decimal Base,
    IReadOnlyList<ImpuestoCalculado> Impuestos);

/// <summary>
/// Un renglón del nodo <c>Impuestos</c> del comprobante, que agrupa lo de todos los
/// conceptos. Los exentos no aparecen aquí: ese nodo exige importe y un exento no lo tiene.
/// </summary>
public sealed record ImpuestoAgrupado(
    string Impuesto,
    string TipoFactor,
    decimal? TasaOCuota,
    decimal Base,
    decimal Importe);

/// <summary>
/// El comprobante calculado, con el detalle por renglón y los totales.
/// </summary>
/// <param name="SubTotal">Suma de los importes <b>antes</b> de descuentos.</param>
/// <param name="Total">SubTotal − Descuento + trasladados − retenidos.</param>
public sealed record ComprobanteCalculado(
    decimal SubTotal,
    decimal Descuento,
    decimal TotalImpuestosTrasladados,
    decimal TotalImpuestosRetenidos,
    decimal Total,
    IReadOnlyList<ConceptoCalculado> Conceptos,
    IReadOnlyList<ImpuestoAgrupado> Traslados,
    IReadOnlyList<ImpuestoAgrupado> Retenciones);

/// <summary>Los tres tipos de factor de <c>c_TipoFactor</c>.</summary>
public static class TiposDeFactor
{
    public const string Tasa = "Tasa";
    public const string Cuota = "Cuota";
    public const string Exento = "Exento";
}

/// <summary>Las claves de <c>c_ObjetoImp</c> que cambian el comportamiento del cálculo.</summary>
public static class ObjetosDeImpuesto
{
    public const string NoObjeto = "01";
    public const string SiObjeto = "02";
    public const string SiObjetoSinDesglose = "03";
}
