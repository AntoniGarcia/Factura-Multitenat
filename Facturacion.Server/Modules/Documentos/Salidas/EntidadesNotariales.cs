namespace Facturacion.Server.Modules.Documentos.Salidas;

/// Notarios Públicos usa la clave 01–32 del INEGI, no c_Estado. La base y la pantalla usan
/// c_Estado, que es contra el que se valida el código postal, y se traduce solo al armar
/// el XML.
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

    // Nulo si no es una entidad de México.
    public static string? ClaveDelComplemento(string? claveEstado)
    {
        var clave = (claveEstado ?? string.Empty).Trim().ToUpperInvariant();
        foreach (var entidad in Entidades)
            if (entidad.ClaveEstado == clave) return entidad.Clave;
        return null;
    }

    // Acepta las dos claves: la vista previa lee c_Estado y el PDF fiscal, la del XML (01–32).
    public static string Nombre(string clave)
    {
        var normalizada = (clave ?? string.Empty).Trim().ToUpperInvariant();
        foreach (var entidad in Entidades)
            if (entidad.ClaveEstado == normalizada || entidad.Clave == normalizada) return entidad.Nombre;
        return clave ?? string.Empty;
    }
}
