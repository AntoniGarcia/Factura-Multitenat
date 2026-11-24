namespace Facturacion.Shared.Operador;

// El saldo y los paquetes exclusivos son de la cuenta, no de la empresa.
public sealed record FichaDeEmpresaDto(
    Guid Id,
    string Rfc,
    string NombreFiscal,
    string RegimenFiscal,
    bool Activa,
    DateTime FechaAltaUtc,
    LicenciasDeEmpresaDto Licencias,
    IReadOnlyList<CompraDeOperadorDto> ComprasPendientes,
    IReadOnlyList<CompraDeOperadorDto> UltimasCompras,
    IReadOnlyList<UsuarioDeCuentaDto> Usuarios);

/// <summary>Módulos contratados para una empresa, administrados únicamente por el operador.</summary>
public sealed record LicenciasDeEmpresaDto(bool Notarios, bool Obras, bool Comercio, bool Ine);

/// <summary>Cambio de módulos contratados por la empresa.</summary>
public sealed record PeticionActualizarLicenciasDeEmpresa(
    bool Notarios,
    bool Obras,
    bool Comercio,
    bool Ine);
