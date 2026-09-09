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
/// Quién está operando el SaaS y qué puede hacer dentro del panel. Lleva los permisos en
/// cada respuesta para que el cliente pueda filtrar el menú y ocultar lo que no aplica;
/// quien de verdad decide sigue siendo el Server. No lleva empresa ni cuenta: el operador
/// no tiene tenencia.
/// </summary>
public sealed record SesionDeOperadorDto(
    Guid OperadorId,
    string Nombre,
    string Correo,
    bool EsPrincipal,
    IReadOnlyList<string> Permisos);

/// <summary>
/// Lo que devuelve iniciar sesión o refrescar. El refresh token no viaja aquí: va en una
/// cookie <c>HttpOnly</c> que el JavaScript de la página no puede leer.
/// </summary>
public sealed record RespuestaSesionDeOperador(
    string AccessToken,
    DateTime ExpiraUtc,
    SesionDeOperadorDto Sesion);
