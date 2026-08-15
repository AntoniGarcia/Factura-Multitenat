namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>Catálogo <c>c_Impuesto</c> (ISR, IVA, IEPS).</summary>
public sealed class SatImpuesto : ISatCatalogoSimple
{
    public required string Clave { get; set; }
    public required string Descripcion { get; set; }
    public bool Retencion { get; set; }
    public bool Traslado { get; set; }
    public required string LocalOFederal { get; set; }
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}
