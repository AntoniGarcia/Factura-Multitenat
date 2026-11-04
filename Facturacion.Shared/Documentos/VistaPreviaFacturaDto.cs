namespace Facturacion.Shared.Documentos;

/// <summary>Cálculo de la captura actual, sin guardar ni asignar folio.</summary>
/// <param name="ImpuestosLocales">Los del complemento de impuestos locales; ya van dentro de <paramref name="Total"/>.</param>
public sealed record VistaPreviaFacturaDto(
    decimal SubTotal,
    decimal Descuento,
    decimal TotalImpuestosTrasladados,
    decimal TotalImpuestosRetenidos,
    decimal Total,
    IReadOnlyList<ConceptoDto> Conceptos,
    IReadOnlyList<ImpuestoLocalDto>? ImpuestosLocales = null);
