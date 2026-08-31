namespace Facturacion.Shared.Plataforma;

/// <summary>Credenciales de inicio de sesión.</summary>
/// <param name="Correo">Correo del usuario.</param>
/// <param name="Contrasena">Contraseña en claro; solo existe durante esta petición.</param>
/// <param name="MantenerSesion">
/// Cambia <b>únicamente</b> la duración de la cookie de refresh: 12 horas sin marcar,
/// 30 días marcado. No se guarda nada más en el navegador (ARQUITECTURA.md §4).
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
/// navegador (ARQUITECTURA.md §8).
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
/// navegador: nunca en <c>localStorage</c> ni en <c>sessionStorage</c> (ARQUITECTURA.md §4).
/// El refresh token no aparece aquí porque viaja en una cookie <c>HttpOnly</c> que el
/// JavaScript de la página no puede leer.
/// </summary>
public sealed record RespuestaSesion(
    string AccessToken,
    DateTime ExpiraUtc,
    SesionDto Sesion);

/// <summary>
/// Cambio de empresa activa. Es el <b>único</b> lugar del sistema donde el Client envía un
/// identificador de empresa (ARQUITECTURA.md §4).
/// </summary>
public sealed record PeticionCambioEmpresa(Guid EmpresaId);

/// <summary>
/// Primer paso del alta de una cuenta nueva desde fuera del sistema. Pide lo mínimo: quién
/// es y dónde recibirlo.
/// <para>
/// <b>Esto todavía no crea nada.</b> Manda un código al correo y deja el alta esperando; la
/// cuenta nace cuando el código vuelve en <see cref="PeticionVerificarAlta"/>. La empresa
/// emisora se da de alta después, ya dentro.
/// </para>
/// </summary>
/// <param name="NombreCuenta">
/// Con que nombre se identifica la cuenta contratante: el del despacho o el del negocio. Si
/// viene vacio se usa el de la persona, que es lo que pasaba cuando el alta era de un solo
/// paso.
/// </param>
public sealed record PeticionRegistro(string Correo, string Nombre, string? NombreCuenta = null);

/// <summary>
/// Respuesta del registro. <b>Es la misma tanto si se mandó el código como si el correo ya
/// estaba registrado</b>: distinguirlas convertiría el registro en un detector de qué
/// correos tienen cuenta aquí. Quien escribió el correo se entera por el correo, no por la
/// pantalla.
/// </summary>
public sealed record RespuestaRegistro(string Mensaje);

/// <summary>
/// Segundo paso del alta: el código que llegó al correo. El correo viaja otra vez porque
/// entre los dos pasos no hay sesión ni cookie donde recordarlo, y el código por sí solo no
/// identifica a nadie.
/// </summary>
public sealed record PeticionVerificarAlta(string Correo, string Codigo);

/// <summary>
/// Resultado de verificar el código. Aquí sí se distingue el éxito del fallo —no habría
/// forma de continuar si no—, pero un código equivocado y un correo sin alta pendiente dan
/// exactamente el mismo error, para no delatar cuál de las dos cosas pasó.
/// </summary>
public sealed record RespuestaAltaVerificada(string Mensaje);
