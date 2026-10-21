namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>El régimen fiscal debe ser compatible con el tipo de persona indicado por el RFC.</summary>
public sealed class SatRegimenFiscal : ISatCatalogoSimple
{
    public required string Clave { get; set; }
    public required string Descripcion { get; set; }
    public bool AplicaFisica { get; set; }
    public bool AplicaMoral { get; set; }
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}
