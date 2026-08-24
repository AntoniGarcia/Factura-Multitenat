namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>
/// Catálogo <c>C_Colonia</c> (~180,000 asentamientos, repartidos en tres hojas en el archivo
/// del SAT porque el formato binario legado no aguanta tantos renglones en una). La clave se
/// repite entre códigos postales, así que la llave real es <see cref="Clave"/> +
/// <see cref="ClaveCodigoPostal"/>.
/// <para>
/// <see cref="Nombre"/> es el otro campo que la búsqueda de código postal indexa con texto
/// completo (ARQUITECTURA.md §7): así "Roma Norte" encuentra el 06700 sin que el capturista
/// necesite saber el código de memoria.
/// </para>
/// </summary>
public sealed class SatColonia
{
    /// <summary>
    /// Identidad numérica de solo uso interno. SQL Server exige que la clave de un índice
    /// de texto completo sea de una sola columna, y la llave real de este catálogo es
    /// compuesta (<see cref="Clave"/> + <see cref="ClaveCodigoPostal"/>); nadie fuera de la
    /// configuración de EF debería leer esta columna.
    /// </summary>
    public int IdInterno { get; set; }

    public required string Clave { get; set; }
    public required string ClaveCodigoPostal { get; set; }
    public required string Nombre { get; set; }
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}
