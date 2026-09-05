using System.ComponentModel.DataAnnotations;

namespace Facturacion.Shared.Operador;

/// <summary>Un usuario de cualquier cuenta, visto desde el panel del proveedor.</summary>
public sealed record UsuarioDePlataformaDto(
    Guid Id,
    string Nombre,
    string Correo,
    bool Activo,
    DateTime FechaAltaUtc,
    Guid CuentaId,
    string CuentaNombre,
    int Empresas,
    bool CreadoPorAdministrador);

/// <summary>Una página de usuarios y cuántos hay en total con ese filtro.</summary>
public sealed record PaginaDeUsuariosDePlataforma(
    IReadOnlyList<UsuarioDePlataformaDto> Elementos,
    int Total);

/// <summary>
/// Restablecimiento de la contraseña de un usuario por parte del operador.
/// <para>
/// Se pide la contraseña del propio operador: es una operación que permite entrar como otra
/// persona, así que no puede quedar a merced de una sesión abierta sin vigilancia.
/// </para>
/// </summary>
public sealed record PeticionRestablecerContrasenaDeUsuario(
    [property: Required, MinLength(12), MaxLength(128)] string ContrasenaNueva,
    [property: Required] string ContrasenaDelOperador);

/// <summary>Alta o baja de un usuario desde el panel, con el motivo para la bitácora.</summary>
public sealed record PeticionCambiarActivoDeUsuario(
    bool Activo,
    [property: MaxLength(300)] string? Motivo);

/// <summary>
/// Quita el acceso de un usuario a una empresa concreta, sin tocar el resto de sus accesos.
/// Baja lógica del nexo <c>UsuarioEmpresa</c>: el usuario deja de poder entrar a esa empresa
/// pero su cuenta sigue viva para las demás.
/// </summary>
public sealed record PeticionEliminarAccesoDeUsuario([property: Required, MaxLength(300)] string Motivo);

/// <summary>
/// Cambio del correo de un usuario por parte del operador. Como toca la identidad de una
/// persona y quién entra con ese correo, exige la contraseña del propio operador, igual que
/// el restablecimiento de contraseña.
/// </summary>
public sealed record PeticionCambiarCorreoDeUsuario(
    [property: Required, EmailAddress, MaxLength(254)] string CorreoNuevo,
    [property: Required] string ContrasenaDelOperador);
