namespace Facturacion.Shared.Plataforma;

/// <summary>Versión publicada por el SAT y fecha de carga de un catálogo (ARQUITECTURA.md §7).</summary>
public sealed record CatalogoVersionDto(
    string Catalogo,
    string VersionCatalogo,
    string RevisionCatalogo,
    DateOnly? FechaPublicacion,
    DateTime FechaCargaUtc,
    int RenglonesVigentes);
