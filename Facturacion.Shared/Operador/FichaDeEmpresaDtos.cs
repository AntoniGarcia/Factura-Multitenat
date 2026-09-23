namespace Facturacion.Shared.Operador;

/// <summary>
/// La ficha de una empresa emisora vista desde el panel: identidad fiscal, consumo de la
/// bolsa, las compras de timbres que esperan acreditarse o rechazarse, las compras pagadas
/// recientes y los usuarios con acceso a esa empresa.
/// </summary>
public sealed record FichaDeEmpresaDto(
    Guid Id,
    string Rfc,
    string NombreFiscal,
    string RegimenFiscal,
    bool Activa,
    DateTime FechaAltaUtc,
    int TimbresDisponibles,
    int TimbresReservados,
    LicenciasDeEmpresaDto Licencias,
    IReadOnlyList<PaquetePersonalizadoDto> PaquetesPersonalizados,
    IReadOnlyList<CompraDeOperadorDto> ComprasPendientes,
    IReadOnlyList<CompraDeOperadorDto> UltimasCompras,
    IReadOnlyList<UsuarioDeCuentaDto> Usuarios);

/// <summary>Módulos contratados para una empresa, administrados únicamente por el operador.</summary>
public sealed record LicenciasDeEmpresaDto(bool Notarios, bool Obras, bool Comercio, bool Ine);

/// <summary>
/// Cambio de módulos contratados. La contraseña vuelve a confirmar una operación comercial
/// que habilita funciones de pago para la empresa.
/// </summary>
public sealed record PeticionActualizarLicenciasDeEmpresa(
    bool Notarios,
    bool Obras,
    bool Comercio,
    bool Ine,
    [property: System.ComponentModel.DataAnnotations.Required] string ContrasenaDelOperador);
