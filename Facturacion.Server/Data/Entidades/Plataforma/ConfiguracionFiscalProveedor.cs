namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>Emisor de las ventas del SaaS. Es global y no pertenece a una cuenta contratante.</summary>
public sealed class ConfiguracionFiscalProveedor
{
    public byte Id { get; set; }
    public required string Rfc { get; set; }
    public required string NombreFiscal { get; set; }
    public required string RegimenFiscal { get; set; }
    public required string CodigoPostalExpedicion { get; set; }
    public required string ZonaHoraria { get; set; }
    public required string Serie { get; set; }
    public DateTime ActualizadaUtc { get; set; }
    public Guid ActualizadaPorOperadorId { get; set; }
    public byte[] Version { get; set; } = [];
}

/// <summary>Conserva los CSD del proveedor; solo el material cifrado llega a SQL.</summary>
public sealed class CertificadoProveedor
{
    public Guid Id { get; set; }
    public byte ConfiguracionId { get; set; }
    public required string Rfc { get; set; }
    public required string NumeroSerie { get; set; }
    public DateTime VigenciaDesdeUtc { get; set; }
    public DateTime VigenciaHastaUtc { get; set; }
    public required byte[] CerCifrado { get; set; }
    public required byte[] KeyCifrada { get; set; }
    public required string ContrasenaCifrada { get; set; }
    public bool Activo { get; set; }
    public DateTime CargadoUtc { get; set; }
    public Guid CargadoPorOperadorId { get; set; }
}
