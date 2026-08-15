namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>
/// Catálogo <c>C_Municipio</c>. La clave se repite entre estados (el "001" de Aguascalientes
/// no es el mismo municipio que el "001" de Baja California), así que la llave real es
/// <see cref="Clave"/> + <see cref="ClaveEstado"/>.
/// <para>
/// Soporte de <see cref="SatCodigoPostal"/>: su <see cref="Descripcion"/> es uno de los dos
/// campos que la búsqueda de código postal indexa con texto completo (CLAUDE.md §7).
/// </para>
/// </summary>
public sealed class SatMunicipio
{
    /// <summary>
    /// Identidad numérica de solo uso interno, por la misma razón que
    /// <see cref="SatColonia.IdInterno"/>: la llave de índice de texto completo tiene que
    /// ser de una sola columna y la llave real de este catálogo es compuesta.
    /// </summary>
    public int IdInterno { get; set; }

    public required string Clave { get; set; }
    public required string ClaveEstado { get; set; }
    public required string Descripcion { get; set; }
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}
