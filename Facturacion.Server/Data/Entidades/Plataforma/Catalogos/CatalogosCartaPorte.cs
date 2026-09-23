namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>
/// Catálogos oficiales de Carta Porte 3.1 usados por los catálogos internos de transporte.
/// Se mantienen separados de los catálogos CFDI porque el SAT los publica en el archivo de
/// Carta Porte, con ciclo de actualización propio.
/// </summary>
public sealed class SatConfiguracionAutotransporte : ISatCatalogoSimple
{
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}

public sealed class SatTipoPermiso : ISatCatalogoSimple
{
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}

public sealed class SatFiguraTransporte : ISatCatalogoSimple
{
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}

/// <summary>Claves de bienes transportados permitidas exclusivamente por Carta Porte.</summary>
public sealed class SatClaveProdServCartaPorte : ISatCatalogoSimple
{
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}
