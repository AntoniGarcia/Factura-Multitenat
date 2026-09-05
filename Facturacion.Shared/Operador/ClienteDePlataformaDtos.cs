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
    DateTime? MembresiaFinUtc,
    int Pendientes);

/// <summary>Una empresa emisora vista desde el panel: identidad fiscal y consumo, nada más.</summary>
public sealed record EmpresaDeCuentaDto(
    Guid Id,
    string Rfc,
    string NombreFiscal,
    string RegimenFiscal,
    bool Activa,
    DateTime FechaAltaUtc,
    int TimbresDisponibles,
    int TimbresReservados,
    int ComprasPendientes);

/// <summary>Un usuario de la cuenta. Sin contraseñas ni permisos: solo quién tiene acceso.</summary>
public sealed record UsuarioDeCuentaDto(
    Guid Id,
    string Nombre,
    string Correo,
    bool Activo,
    DateTime FechaAltaUtc);

/// <summary>
/// La ficha de una cuenta contratante: sus datos y sus empresas. Los usuarios con acceso y las
/// compras de cada empresa se ven en la ficha de esa empresa, no mezclados aquí.
/// </summary>
public sealed record DetalleDeCuentaDto(
    CuentaEnListaDto Cuenta,
    IReadOnlyList<EmpresaDeCuentaDto> Empresas);

/// <summary>Una página de cuentas y cuántas hay en total con ese filtro.</summary>
public sealed record PaginaDeCuentas(
    IReadOnlyList<CuentaEnListaDto> Elementos,
    int Total);

/// <summary>
/// Alta o baja lógica de una empresa desde el panel. El motivo es obligatorio al desactivar:
/// es lo único que explicará después por qué se le quitó el servicio a esa exportación, como
/// el motivo de una baja de cliente.
/// </summary>
public sealed record PeticionCambiarActivoDeEmpresa(
    bool Activo,
    [property: System.ComponentModel.DataAnnotations.MaxLength(300)] string? Motivo,
    [property: System.ComponentModel.DataAnnotations.Required] string ContrasenaDelOperador);

/// <summary>
/// Nuevo correo de contacto de una cuenta. Es un dato administrativo del SaaS, no un inicio de
/// sesión: cambiar aquí el correo no cambia las credenciales de nadie.
/// </summary>
public sealed record PeticionCambiarCorreoDeContactoDeCuenta(
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.EmailAddress]
    [property: System.ComponentModel.DataAnnotations.MaxLength(254)]
    string CorreoNuevo,
    [property: System.ComponentModel.DataAnnotations.Required] string ContrasenaDelOperador);
