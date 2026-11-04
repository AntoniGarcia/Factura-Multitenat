using System.ComponentModel.DataAnnotations;

namespace Facturacion.Shared.Operador;

public sealed record ConfiguracionFiscalProveedorDto(
    string Rfc,
    string NombreFiscal,
    string RegimenFiscal,
    string CodigoPostalExpedicion,
    string ZonaHoraria,
    string Serie,
    byte[]? Version,
    IReadOnlyList<CertificadoProveedorDto> Certificados);

public sealed record CertificadoProveedorDto(
    Guid Id,
    string NumeroSerie,
    DateTime VigenciaDesdeUtc,
    DateTime VigenciaHastaUtc,
    bool Activo,
    DateTime CargadoUtc);

public sealed record PeticionGuardarFiscalProveedor(
    [property: Required, MaxLength(13)] string Rfc,
    [property: Required, MaxLength(254)] string NombreFiscal,
    [property: Required, StringLength(3)] string RegimenFiscal,
    [property: Required, StringLength(5)] string CodigoPostalExpedicion,
    [property: Required, MaxLength(64)] string ZonaHoraria,
    [property: Required, MaxLength(25)] string Serie,
    byte[]? Version,
    [property: Required, MaxLength(128)] string ContrasenaDelOperador);
