namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>
/// El complemento de Notarios Públicos no usa <c>c_Estado</c>: su <c>t_EntidadFederativa</c> es la
/// clave numérica 01–32 del INEGI. La pantalla y la base trabajan con <c>c_Estado</c>, que es el
/// catálogo contra el que se valida el código postal, y la traducción ocurre solo al armar el XML.
/// </summary>
public static class EntidadesNotariales
{
    private static readonly IReadOnlyList<(string ClaveEstado, string Clave, string Nombre)> Entidades =
    [
        ("AGU", "01", "Aguascalientes"),
        ("BCN", "02", "Baja California"),
        ("BCS", "03", "Baja California Sur"),
        ("CAM", "04", "Campeche"),
        ("COA", "05", "Coahuila"),
        ("COL", "06", "Colima"),
        ("CHP", "07", "Chiapas"),
        ("CHH", "08", "Chihuahua"),
        ("CMX", "09", "Ciudad de México"),
        ("DUR", "10", "Durango"),
        ("GUA", "11", "Guanajuato"),
        ("GRO", "12", "Guerrero"),
        ("HID", "13", "Hidalgo"),
        ("JAL", "14", "Jalisco"),
        ("MEX", "15", "Estado de México"),
        ("MIC", "16", "Michoacán"),
        ("MOR", "17", "Morelos"),
        ("NAY", "18", "Nayarit"),
        ("NLE", "19", "Nuevo León"),
        ("OAX", "20", "Oaxaca"),
        ("PUE", "21", "Puebla"),
        ("QUE", "22", "Querétaro"),
        ("ROO", "23", "Quintana Roo"),
        ("SLP", "24", "San Luis Potosí"),
        ("SIN", "25", "Sinaloa"),
        ("SON", "26", "Sonora"),
        ("TAB", "27", "Tabasco"),
        ("TAM", "28", "Tamaulipas"),
        ("TLA", "29", "Tlaxcala"),
        ("VER", "30", "Veracruz"),
        ("YUC", "31", "Yucatán"),
        ("ZAC", "32", "Zacatecas")
    ];

    /// <summary>Clave del complemento para una clave de <c>c_Estado</c>; null si no es una entidad de México.</summary>
    public static string? ClaveDelComplemento(string? claveEstado)
    {
        var clave = (claveEstado ?? string.Empty).Trim().ToUpperInvariant();
        foreach (var entidad in Entidades)
            if (entidad.ClaveEstado == clave) return entidad.Clave;
        return null;
    }

    /// <summary>
    /// Nombre para imprimir. Acepta las dos claves porque la vista previa lee la base (<c>c_Estado</c>)
    /// y el PDF fiscal lee el XML timbrado (01–32).
    /// </summary>
    public static string Nombre(string clave)
    {
        foreach (var entidad in Entidades)
            if (entidad.ClaveEstado == clave || entidad.Clave == clave) return entidad.Nombre;
        return clave;
    }
}
