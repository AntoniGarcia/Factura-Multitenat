namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

public sealed class SatMaterialPeligroso : ISatCatalogoSimple
{
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}

public sealed class SatTipoEmbalaje : ISatCatalogoSimple
{
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}
