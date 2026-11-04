namespace Facturacion.Shared.Operador;

public sealed record EmisorFacturaCompraDto(
    string Rfc, string NombreFiscal, string RegimenFiscal,
    string CodigoPostalExpedicion, string ZonaHoraria, string Serie);

public sealed record ReceptorFacturaCompraDto(
    string Rfc, string NombreFiscal, string RegimenFiscal, string CodigoPostalFiscal, string UsoCfdi);

public sealed record ConceptoFacturaCompraDto(
    string Descripcion, int CantidadTimbres, string ClaveProdServ, string ClaveUnidad,
    decimal Subtotal, decimal Iva, decimal TasaIva, decimal Total);

public sealed record FacturaDeCompraDto(
    Guid? Id, Guid CompraId, string Estado,
    EmisorFacturaCompraDto Emisor, ReceptorFacturaCompraDto Receptor, ConceptoFacturaCompraDto Concepto,
    string FormaPago, byte[]? Version, byte[] VersionProveedor)
{
    public string MetodoPago => "PUE";
    public string Moneda => "MXN";
    public int? Folio { get; init; }
    public Guid? Uuid { get; init; }
    public DateTime? FechaTimbradoUtc { get; init; }
}

public sealed record PeticionTimbrarFacturaCompra(byte[] Version);
public sealed record SolicitudFacturaCompraDto(Guid IntentoId);

// Los importes y el emisor se obtienen de SQL; no se reciben del navegador.
public sealed record PeticionGuardarFacturaCompra(
    string NombreReceptor, string RegimenReceptor, string CodigoPostalReceptor, string UsoCfdi,
    string Descripcion, string ClaveProdServ, string ClaveUnidad, string FormaPago,
    byte[]? Version, byte[] VersionProveedor);
