namespace Facturacion.Shared.Plataforma;

/// <summary>Credenciales de inicio de sesión.</summary>
/// <param name="Correo">Correo del usuario.</param>
/// <param name="Contrasena">Contraseña en claro; solo existe durante esta petición.</param>
/// <param name="MantenerSesion">
/// Cambia <b>únicamente</b> la duración de la cookie de refresh: 12 horas sin marcar,
/// 30 días marcado. No se guarda nada más en el navegador (CLAUDE.md §4).
/// </param>
public sealed record PeticionInicioSesion(
    string Correo,
    string Contrasena,
    bool MantenerSesion);

/// <summary>Empresa a la que el usuario puede cambiarse.</summary>
public sealed record EmpresaDisponibleDto(
    Guid Id,
    string NombreFiscal,
    string Rfc);

/// <summary>Quién es el usuario y qué puede hacer ahora mismo.</summary>
/// <param name="UsuarioId">Usuario autenticado.</param>
/// <param name="Nombre">Nombre para mostrar.</param>
/// <param name="Correo">Correo del usuario.</param>
/// <param name="EmpresaActivaId">
/// Empresa activa, o <c>null</c> si tiene varias y todavía no elige una.
/// </param>
/// <param name="Empresas">Empresas a las que tiene acceso dentro de su cuenta.</param>
/// <param name="Permisos">Permisos en la empresa activa. Vacío mientras no haya empresa.</param>
/// <param name="Tema">
/// Clave de <c>Facturacion.Shared.Comun.Temas</c>. El Client la aplica al arrancar, antes
/// de que el usuario vea nada: el tema es parte de su perfil, no una preferencia del
/// navegador (CLAUDE.md §8).
/// </param>
public sealed record SesionDto(
    Guid UsuarioId,
    string Nombre,
    string Correo,
    Guid? EmpresaActivaId,
    IReadOnlyList<EmpresaDisponibleDto> Empresas,
    IReadOnlyList<string> Permisos,
    string Tema);

/// <summary>Cambio de tema. La clave se valida contra <c>Temas.Todos</c> en el servidor.</summary>
public sealed record PeticionCambioTema(string Tema);

/// <summary>
/// Access token nuevo y la sesión que describe. El token vive <b>solo en memoria</b> del
/// navegador: nunca en <c>localStorage</c> ni en <c>sessionStorage</c> (CLAUDE.md §4).
/// El refresh token no aparece aquí porque viaja en una cookie <c>HttpOnly</c> que el
/// JavaScript de la página no puede leer.
/// </summary>
public sealed record RespuestaSesion(
    string AccessToken,
    DateTime ExpiraUtc,
    SesionDto Sesion);

/// <summary>
/// Cambio de empresa activa. Es el <b>único</b> lugar del sistema donde el Client envía un
/// identificador de empresa (CLAUDE.md §4).
/// </summary>
public sealed record PeticionCambioEmpresa(Guid EmpresaId);
