namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>
/// Versión publicada por el SAT y fecha de carga de cada catálogo (ARQUITECTURA.md §7). Un
/// renglón por catálogo, actualizado por <c>ImportadorCatalogosSat</c> cada vez que corre.
/// </summary>
public sealed class CatalogoVersion
{
    /// <summary>Nombre del catálogo, por ejemplo <c>c_ClaveProdServ</c>. Es la clave.</summary>
    public required string Catalogo { get; set; }

    public required string VersionCatalogo { get; set; }

    public required string RevisionCatalogo { get; set; }

    public DateOnly? FechaPublicacion { get; set; }

    public DateTime FechaCargaUtc { get; set; }

    /// <summary>Cuántos renglones vigentes quedaron después de esta carga.</summary>
    public int RenglonesVigentes { get; set; }
}
