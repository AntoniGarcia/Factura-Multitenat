namespace Facturacion.Shared.Operador;

public sealed record ActualizacionCatalogosDto(
    Guid Id, string Catalogo, string Estado, string Mensaje, DateTime InicioUtc, DateTime? FinUtc = null);
