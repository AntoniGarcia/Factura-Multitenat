namespace Facturacion.Shared.Documentos;

/// <summary>Cálculo de la captura actual, sin guardar ni asignar folio.</summary>
public sealed record VistaPreviaFacturaDto(
    decimal SubTotal,
    decimal Descuento,
    decimal TotalImpuestosTrasladados,
    decimal TotalImpuestosRetenidos,
    decimal Total,
    IReadOnlyList<ConceptoDto> Conceptos);
