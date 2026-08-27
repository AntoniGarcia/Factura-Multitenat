using System.ComponentModel.DataAnnotations;

namespace Facturacion.Shared.Operador;

/// <summary>
/// Cambio del correo con el que el operador entra al panel.
/// <para>
/// Pide la contraseña actual: quien se siente un momento frente a una sesión abierta no puede
/// quedarse con la cuenta cambiándole el correo.
/// </para>
/// </summary>
public sealed record PeticionCambiarCorreoOperador(
    [property: Required, EmailAddress, MaxLength(254)] string Correo,
    [property: Required] string ContrasenaActual);

/// <summary>
/// Cambio de contraseña. Exige la actual por lo mismo, y además cierra las demás sesiones.
/// </summary>
public sealed record PeticionCambiarContrasenaOperador(
    [property: Required] string ContrasenaActual,
    [property: Required, MinLength(12), MaxLength(128)] string ContrasenaNueva);

/// <summary>Datos editables del propio operador.</summary>
public sealed record PeticionActualizarPerfilOperador(
    [property: Required, MaxLength(128)] string Nombre);
