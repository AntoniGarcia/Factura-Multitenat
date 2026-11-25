namespace Facturacion.Shared.Documentos;

// Cálculo de la captura sin guardar ni asignar folio. ImpuestosLocales ya van dentro de Total.
public sealed record VistaPreviaFacturaDto(
    decimal SubTotal,
    decimal Descuento,
    decimal TotalImpuestosTrasladados,
    decimal TotalImpuestosRetenidos,
    decimal Total,
    IReadOnlyList<ConceptoDto> Conceptos,
    IReadOnlyList<ImpuestoLocalDto>? ImpuestosLocales = null);
