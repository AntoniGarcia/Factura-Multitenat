using System.ComponentModel.DataAnnotations;

namespace Facturacion.Shared.Operador;

/// <summary>Un operador del SaaS visto desde el panel.</summary>
public sealed record OperadorDto(
    Guid Id,
    string Nombre,
    string Correo,
    bool Activo,
    bool EsPrincipal,
    DateTime FechaAltaUtc,
    IReadOnlyList<string> Permisos);

/// <summary>Una página de operadores y cuántos hay en total con ese filtro.</summary>
public sealed record PaginaDeOperadores(
    IReadOnlyList<OperadorDto> Elementos,
    int Total);

/// <summary>Alta o edición de un operador.</summary>
public sealed record PeticionGuardarOperador(
    [property: Required, EmailAddress, MaxLength(254)] string Correo,
    [property: Required, MaxLength(128)] string Nombre,
    [property: Required, MinLength(12), MaxLength(128)] string Contrasena,
    [property: Required] IReadOnlyList<string> Permisos);

/// <summary>Cambio de permisos de un operador.</summary>
public sealed record PeticionPermisosOperador(
    [property: Required] IReadOnlyList<string> Permisos);

/// <summary>Alta o baja lógica de un operador.</summary>
public sealed record PeticionCambiarActivoOperador(
    bool Activo,
    [property: MaxLength(300)] string? Motivo);

/// <summary>
/// Contraseña nueva que el operador principal le pone a otro operador. Solo él puede:
/// es entrar como otra persona, la operación más delicada del panel.
/// </summary>
public sealed record PeticionContrasenaNuevaDeOperador(
    [property: Required, MinLength(12), MaxLength(128)] string ContrasenaNueva);