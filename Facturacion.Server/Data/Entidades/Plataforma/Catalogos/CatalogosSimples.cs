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

/// <summary>
/// Los catálogos del SAT cuya única información, además de la clave y la descripción, es su
/// vigencia. Cada uno es su propia tabla —CLAUDE.md §7 pide justo eso, no una tabla
/// clave-valor genérica— pero como no tienen columnas propias que los distingan, viven
/// juntos en un solo archivo en vez de repetir la misma forma ocho veces.
/// <para>
/// Si el SAT le agrega una columna propia a alguno de estos catálogos, ese catálogo sale de
/// aquí a su propio archivo: en cuanto deja de ser "solo clave y descripción" deja de encajar
/// en este grupo.
/// </para>
/// <para>
/// Sin <c>required</c> en <see cref="ISatCatalogoSimple.Clave"/>/<c>Descripcion</c>: el
/// importador las construye con <c>new TEntidad()</c> genérico, y el compilador no puede
/// verificar miembros requeridos a través de un parámetro de tipo. El único código que crea
/// estas entidades es el importador, que siempre las llena por completo.
/// </para>
/// </summary>
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
