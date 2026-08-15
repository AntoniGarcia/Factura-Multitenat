namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>Catálogo <c>c_ClaveUnidad</c>. La clave es alfanumérica (por ejemplo <c>H87</c>, <c>KGM</c>).</summary>
public sealed class SatClaveUnidad
{
    public required string Clave { get; set; }
    public required string Nombre { get; set; }
    public string? Descripcion { get; set; }
    public string? Nota { get; set; }
    public string? Simbolo { get; set; }
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}
