namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>Catálogo <c>c_Estado</c>. Soporte de <see cref="SatCodigoPostal"/>: resuelve el nombre del estado.</summary>
public sealed class SatEstado
{
    public required string Clave { get; set; }
    public required string ClavePais { get; set; }
    public required string Nombre { get; set; }
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}
