namespace Facturacion.Shared.Plataforma;

/// <summary>Un usuario con acceso a la empresa activa, y los permisos que tiene en ELLA.</summary>
/// <param name="CreadoPorAdministrador">
/// Su contraseña la fijó un administrador y solo un administrador la cambia. La pantalla lo
/// muestra para que quede claro de quién es cada acceso.
/// </param>
public sealed record UsuarioDeEmpresaDto(
    Guid UsuarioId,
    string Nombre,
    string Correo,
    bool Activo,
    bool EsUsuarioActual,
    bool CreadoPorAdministrador,
    IReadOnlyList<string> Permisos);

public sealed record UsuariosDeLaEmpresaDto(
    IReadOnlyList<UsuarioDeEmpresaDto> Usuarios,
    int LimitePorEmpresa);

/// <summary>
/// Alta de un usuario por parte de un administrador. Sustituyó a la invitación por correo:
/// el administrador teclea la contraseña y se la comunica por fuera del sistema.
///
/// <para>
/// La contraseña la valida ASP.NET Core Identity con sus reglas de longitud y composición
/// (ARQUITECTURA.md §4). El servidor no la devuelve nunca, ni siquiera al que la acaba de escribir.
/// </para>
/// </summary>
public sealed record PeticionCrearUsuario(
    string Correo,
    string Nombre,
    string Contrasena,
    IReadOnlyList<string> Permisos);

/// <summary>
/// Qué pasó al dar de alta. <c>AccesoDirecto</c> es <c>true</c> cuando el correo ya era de un
/// usuario de la misma cuenta: no se creó a nadie, solo se le dio acceso a esta empresa, y la
/// contraseña que se haya tecleado se ignora porque ese usuario ya tiene la suya.
/// </summary>
public sealed record RespuestaCrearUsuario(bool AccesoDirecto, string Mensaje);

/// <summary>Cambio del correo de otro usuario, hecho por un administrador.</summary>
public sealed record PeticionCambiarCorreoUsuario(string Correo);

/// <summary>
/// El administrador le pone una contraseña nueva a un usuario que él dio de alta. No pide la
/// anterior: el sentido de esto es precisamente que el usuario la olvidó, o que dejó de
/// trabajar aquí y hay que cerrarle el paso.
/// </summary>
public sealed record PeticionCambiarContrasenaUsuario(string Contrasena);

public sealed record PeticionActualizarPermisos(IReadOnlyList<string> Permisos);

public sealed record PeticionCambiarActivoUsuario(bool Activo);

/// <param name="PuedeCambiarContrasena">
/// Falso para el usuario que creó un administrador: su contraseña la administra él. La
/// pantalla de perfil esconde el formulario, y el servidor lo rechaza igual — esconder un
/// botón no es proteger (ARQUITECTURA.md §4).
/// </param>
public sealed record PerfilDto(string Nombre, string Correo, string Tema, bool PuedeCambiarContrasena);

public sealed record PeticionActualizarPerfil(string Nombre);

public sealed record PeticionCambiarContrasena(string ContrasenaActual, string ContrasenaNueva);
