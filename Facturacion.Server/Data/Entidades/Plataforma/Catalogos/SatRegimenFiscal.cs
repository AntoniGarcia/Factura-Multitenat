namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>
/// Catálogo <c>c_RegimenFiscal</c>. <see cref="AplicaFisica"/>/<see cref="AplicaMoral"/> son
/// la validación de CLAUDE.md §7: el régimen tiene que ser compatible con el tipo de persona
/// que implica la longitud del RFC.
/// </summary>
public sealed class SatRegimenFiscal : ISatCatalogoSimple
{
    public required string Clave { get; set; }
    public required string Descripcion { get; set; }
    public bool AplicaFisica { get; set; }
    public bool AplicaMoral { get; set; }
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}
