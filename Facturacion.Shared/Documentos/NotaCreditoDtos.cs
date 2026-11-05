namespace Facturacion.Shared.Documentos;

public sealed record OrigenNotaCreditoDto(Guid Uuid, string? Serie, int? Folio, string Rfc, string Nombre,
    string Moneda, decimal TotalOriginal, IReadOnlyList<ConceptoAcreditableDto> Conceptos)
{
    public decimal? SaldoPendiente { get; init; }
}

public sealed record ConceptoAcreditableDto(int Orden, string Descripcion, decimal BaseOriginal, decimal BaseDisponible);

public sealed record NotaCreditoDto(ComprobanteDto Comprobante, OrigenNotaCreditoDto Origen, bool PuedeEditar);

public sealed record PeticionCrearNotaCredito(Guid UuidFactura);

public sealed record PeticionGuardarNotaCredito(Guid? SerieId, string FormaPago, string TipoRelacion,
    string? Observaciones, IReadOnlyList<RenglonNotaCreditoDto> Conceptos);

public sealed record RenglonNotaCreditoDto(int OrdenOriginal, decimal BaseAcreditar, string? Descripcion = null);
