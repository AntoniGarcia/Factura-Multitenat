using Facturacion.Server.Data.Entidades.Documentos;

namespace Facturacion.Server.Data.Entidades.Transporte;

/// <summary>Datos propios del complemento Carta Porte de un CFDI de traslado.</summary>
public sealed class TrasladoCartaPorte : IEntidadDeEmpresa
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid ComprobanteId { get; set; }
    public Comprobante Comprobante { get; set; } = null!;
    public Guid VehiculoId { get; set; }
    public Guid FiguraTransporteId { get; set; }
    public Guid? ClienteDestinoId { get; set; }
    /// <summary>Identificador requerido por Carta Porte 3.1, estable desde el borrador.</summary>
    public string? IdCcp { get; set; }
    // Copias de los datos del catálogo maestro que el complemento usa.
    public string? VehiculoConfiguracionAutotransporte { get; set; }
    public string? VehiculoPlaca { get; set; }
    public int? VehiculoAnioModelo { get; set; }
    public decimal? VehiculoPesoBruto { get; set; }
    public string? VehiculoAseguradora { get; set; }
    public string? VehiculoPoliza { get; set; }
    public string? AseguraMedAmbiente { get; set; }
    public string? PolizaMedAmbiente { get; set; }
    public string? VehiculoTipoPermiso { get; set; }
    public string? VehiculoNumeroPermiso { get; set; }
    public string? FiguraTipo { get; set; }
    public string? FiguraRfc { get; set; }
    public string? FiguraNombre { get; set; }
    public string? FiguraNumeroLicencia { get; set; }
    public DateTime FechaSalidaUtc { get; set; }
    public DateTime FechaLlegadaUtc { get; set; }
    public decimal DistanciaRecorridaKm { get; set; }
    public decimal PesoBrutoTotalKg { get; set; }
    public decimal TotalMercancias { get; set; }
    public List<UbicacionCartaPorte> Ubicaciones { get; set; } = [];
    public List<MercanciaCartaPorte> Mercancias { get; set; } = [];
    public List<RemolqueCartaPorte> Remolques { get; set; } = [];
}

public sealed class RemolqueCartaPorte : IEntidadDeEmpresa
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid TrasladoCartaPorteId { get; set; }
    public TrasladoCartaPorte TrasladoCartaPorte { get; set; } = null!;
    public int Orden { get; set; }
    public required string SubTipoRem { get; set; }
    public required string Placa { get; set; }
}

public sealed class UbicacionCartaPorte : IEntidadDeEmpresa
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid TrasladoCartaPorteId { get; set; }
    public TrasladoCartaPorte TrasladoCartaPorte { get; set; } = null!;
    public required string Tipo { get; set; }
    public int Orden { get; set; }
    public string? RfcRemitenteDestinatario { get; set; }
    public string? NombreRemitenteDestinatario { get; set; }
    public required string Calle { get; set; }
    public required string NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public required string Estado { get; set; }
    public required string Municipio { get; set; }
    public required string CodigoPostal { get; set; }
}

public sealed class MercanciaCartaPorte : IEntidadDeEmpresa
{
    public string? MaterialPeligroso { get; set; }
    public string? CveMaterialPeligroso { get; set; }
    public string? Embalaje { get; set; }
    public string? DescripEmbalaje { get; set; }
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid TrasladoCartaPorteId { get; set; }
    public TrasladoCartaPorte TrasladoCartaPorte { get; set; } = null!;
    public int Orden { get; set; }
    public required string ClaveProdServ { get; set; }
    public required string Descripcion { get; set; }
    public decimal Cantidad { get; set; }
    public required string ClaveUnidad { get; set; }
    public decimal PesoEnKg { get; set; }
    public decimal? PesoUnitarioKg { get; set; }
    public string? Unidad { get; set; }
    public string? Dimensiones { get; set; }
}
