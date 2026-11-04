namespace Facturacion.Server.Data.Entidades.Plataforma;

public sealed class SerieFacturaProveedor
{
    public required string Rfc { get; set; }
    public required string Serie { get; set; }
    public int UltimoFolio { get; set; }
}

public sealed class IntentoFacturaProveedor : IEntidadDeCuenta
{
    public Guid Id { get; set; }
    public Guid CuentaId { get; set; }
    public Guid FacturaId { get; set; }
    public Guid CertificadoId { get; set; }
    public Guid OperadorId { get; set; }
    public required string ClaveSolicitud { get; set; }
    public required byte[] VersionSolicitada { get; set; }
    public Guid Control { get; set; }
    public required string Estado { get; set; }
    public int Folio { get; set; }
    public DateTime FechaEmisionUtc { get; set; }
    public DateTime ActualizadaUtc { get; set; }
    public string? XmlEnviado { get; set; }
    public string? CadenaOriginal { get; set; }
    public string? Sello { get; set; }
    public string? NoCertificado { get; set; }
    public string? CodigoError { get; set; }
    public string? XmlTimbrado { get; set; }
    public Guid? Uuid { get; set; }
    public DateTime? FechaTimbradoUtc { get; set; }
    public string? NoCertificadoSat { get; set; }
    public string? SelloSat { get; set; }
    public string? CadenaOriginalSat { get; set; }
}
