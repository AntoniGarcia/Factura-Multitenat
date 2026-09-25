namespace Facturacion.Server.Data.Entidades.Documentos;

public sealed class DatosObra : IEntidadDeEmpresa
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid ComprobanteId { get; set; }
    public Comprobante Comprobante { get; set; } = null!;
    public string? TipoObra { get; set; }
    public decimal PorcentajeAmortizacion { get; set; }
    public decimal? PorcentajeRetenciones { get; set; }
    public decimal Retenciones { get; set; }
    public decimal? PorcentajeDevoluciones { get; set; }
    public decimal Devoluciones { get; set; }
    public decimal PorcentajeIva { get; set; }
    public string NombreDeduccion1 { get; set; } = "5 al millar";
    public decimal PorcentajeDeduccion1 { get; set; }
    public string NombreDeduccion2 { get; set; } = "1% OBS";
    public decimal PorcentajeDeduccion2 { get; set; }
    public string NombreDeduccion3 { get; set; } = "ICIC";
    public decimal PorcentajeDeduccion3 { get; set; }
    public string NombreDeduccion4 { get; set; } = "ÚNETE";
    public decimal PorcentajeDeduccion4 { get; set; }
}
