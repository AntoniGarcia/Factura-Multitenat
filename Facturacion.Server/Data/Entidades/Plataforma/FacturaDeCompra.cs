namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>Factura del proveedor a la cuenta compradora, con datos fiscales e importes propios.</summary>
public sealed class FacturaDeCompra : IEntidadDeCuenta
{
    public Guid Id { get; set; }
    public Guid CompraId { get; set; }
    public Guid CuentaId { get; set; }
    public Guid EmpresaId { get; set; }
    public string Estado { get; set; } = "borrador";
    public int? Folio { get; set; }
    public Guid? Uuid { get; set; }
    public DateTime? FechaTimbradoUtc { get; set; }
    public required string EmisorRfc { get; set; }
    public required string EmisorNombre { get; set; }
    public required string EmisorRegimen { get; set; }
    public required string EmisorCodigoPostal { get; set; }
    public required string EmisorZonaHoraria { get; set; }
    public required string Serie { get; set; }
    public required byte[] VersionProveedor { get; set; }
    public required string ReceptorRfc { get; set; }
    public required string ReceptorNombre { get; set; }
    public required string ReceptorRegimen { get; set; }
    public required string ReceptorCodigoPostal { get; set; }
    public required string UsoCfdi { get; set; }
    public required string Descripcion { get; set; }
    public int CantidadTimbres { get; set; }
    public required string ClaveProdServ { get; set; }
    public required string ClaveUnidad { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal TasaIva { get; set; }
    public decimal Total { get; set; }
    public required string FormaPago { get; set; }
    public DateTime CreadaUtc { get; set; }
    public DateTime ActualizadaUtc { get; set; }
    public Guid CreadaPorOperadorId { get; set; }
    public Guid ActualizadaPorOperadorId { get; set; }
    public byte[] Version { get; set; } = [];
}
