namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>
/// Catálogo <c>c_TipoFactor</c> (Tasa, Cuota, Exento). El archivo del SAT no le da una
/// columna de descripción aparte: la clave ya es la palabra completa.
/// </summary>
public sealed class SatTipoFactor
{
    public required string Clave { get; set; }
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}
