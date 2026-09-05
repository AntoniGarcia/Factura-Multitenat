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
    IReadOnlyList<CompraDeOperadorDto> ComprasPendientes,
    IReadOnlyList<CompraDeOperadorDto> UltimasCompras,
    IReadOnlyList<UsuarioDeCuentaDto> Usuarios);