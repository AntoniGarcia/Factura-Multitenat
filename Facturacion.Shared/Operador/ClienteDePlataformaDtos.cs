namespace Facturacion.Shared.Operador;

/// <summary>
/// Una cuenta contratante en el listado del panel.
///
/// <para><b>Qué NO lleva, a propósito</b></para>
/// Ningún dato fiscal del inquilino: ni sus clientes, ni sus productos, ni sus comprobantes.
/// El proveedor del SaaS necesita saber cuánto le vende a cada cuenta y si sigue activa, no
/// a quién le factura ella. Es la línea que separa administrar el servicio de mirar dentro
/// del negocio ajeno.
/// </summary>
public sealed record CuentaEnListaDto(
    Guid Id,
    string Nombre,
    string CorreoContacto,
    bool Activa,
    DateTime FechaAltaUtc,
    int Empresas,
    int Usuarios,
    int TimbresDisponibles,
    string? MembresiaEstado,
    DateTime? MembresiaFinUtc);

/// <summary>Una empresa emisora vista desde el panel: identidad fiscal y consumo, nada más.</summary>
public sealed record EmpresaDeCuentaDto(
    Guid Id,
    string Rfc,
    string NombreFiscal,
    string RegimenFiscal,
    bool Activa,
    DateTime FechaAltaUtc,
    int TimbresDisponibles,
    int TimbresReservados);

/// <summary>Un usuario de la cuenta. Sin contraseñas ni permisos: solo quién tiene acceso.</summary>
public sealed record UsuarioDeCuentaDto(
    Guid Id,
    string Nombre,
    string Correo,
    bool Activo,
    DateTime FechaAltaUtc);

/// <summary>La ficha completa de una cuenta contratante.</summary>
public sealed record DetalleDeCuentaDto(
    CuentaEnListaDto Cuenta,
    IReadOnlyList<EmpresaDeCuentaDto> Empresas,
    IReadOnlyList<UsuarioDeCuentaDto> Usuarios,
    IReadOnlyList<CompraDeOperadorDto> UltimasCompras);

/// <summary>Una página de cuentas y cuántas hay en total con ese filtro.</summary>
public sealed record PaginaDeCuentas(
    IReadOnlyList<CuentaEnListaDto> Elementos,
    int Total);
