using System.ComponentModel.DataAnnotations;

namespace Facturacion.Shared.Operador;

/// <summary>
/// Credenciales del panel de operador. No hay alta pública: los operadores se crean por
/// consola, así que no existe un DTO de registro.
/// </summary>
public sealed record PeticionInicioSesionOperador(
    [property: Required, EmailAddress] string Correo,
    [property: Required] string Contrasena,
    bool MantenerSesion = false);

/// <summary>
/// Quién está operando el SaaS. Deliberadamente no lleva empresa, cuenta ni permisos: el
/// operador no tiene tenencia, y dentro del panel lo ve todo.
/// </summary>
public sealed record SesionDeOperadorDto(
    Guid OperadorId,
    string Nombre,
    string Correo);

/// <summary>
/// Lo que devuelve iniciar sesión o refrescar. El refresh token no viaja aquí: va en una
/// cookie <c>HttpOnly</c> que el JavaScript de la página no puede leer.
/// </summary>
public sealed record RespuestaSesionDeOperador(
    string AccessToken,
    DateTime ExpiraUtc,
    SesionDeOperadorDto Sesion);
