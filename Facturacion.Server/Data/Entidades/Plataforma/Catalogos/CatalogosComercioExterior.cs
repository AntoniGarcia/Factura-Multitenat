namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

public sealed class SatIncoterm : ISatCatalogoSimple
{
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}

public sealed class SatUnidadAduana : ISatCatalogoSimple
{
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}

public sealed class SatFraccionArancelaria : ISatCatalogoSimple
{
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}
