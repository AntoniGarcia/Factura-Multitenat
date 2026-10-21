namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>
/// Forma común de los catálogos del SAT que no tienen más columna propia que la vigencia.
/// La usa <c>ImportadorCatalogosSat</c> para leerlos y sincronizarlos los ocho con el mismo
/// código genérico, en vez de repetirlo ocho veces.
/// </summary>
public interface ISatCatalogoSimple
{
    string Clave { get; set; }
    string Descripcion { get; set; }
    DateOnly FechaInicioVigencia { get; set; }
    DateOnly? FechaFinVigencia { get; set; }
    bool Vigente { get; set; }
}

/// <summary>Catálogos con clave, descripción y vigencia, cada uno en su tabla. No usan required porque el importador los crea con new TEntidad() y después asigna sus datos.</summary>
public sealed class SatFormaPago : ISatCatalogoSimple
{
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}

public sealed class SatExportacion : ISatCatalogoSimple
{
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}

public sealed class SatMetodoPago : ISatCatalogoSimple
{
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}

public sealed class SatPeriodicidad : ISatCatalogoSimple
{
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}

public sealed class SatMes : ISatCatalogoSimple
{
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}

public sealed class SatTipoRelacion : ISatCatalogoSimple
{
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}

public sealed class SatPais : ISatCatalogoSimple
{
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}

public sealed class SatObjetoImp : ISatCatalogoSimple
{
    public string Clave { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}
