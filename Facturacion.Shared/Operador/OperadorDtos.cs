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

/// <summary>
/// Alta o edición de un operador. En edición la contraseña se puede dejar vacía para
/// conservar la actual; si se manda, es la operación delicada que solo el principal puede
/// hacer sobre otro operador.
/// </summary>
public sealed record PeticionGuardarOperador(
    [property: Required, EmailAddress, MaxLength(254)] string Correo,
    [property: Required, MaxLength(128)] string Nombre,
    [property: MaxLength(128)] string Contrasena,
    [property: Required] IReadOnlyList<string> Permisos);

/// <summary>Alta o baja lógica de un operador.</summary>
public sealed record PeticionCambiarActivoOperador(
    bool Activo,
    [property: MaxLength(300)] string? Motivo);