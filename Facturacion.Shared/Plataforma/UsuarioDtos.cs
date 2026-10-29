namespace Facturacion.Shared.Plataforma;

/// <summary>
/// Un usuario de la cuenta. Sus permisos son los mismos en todas las empresas a las que entra
/// (AGENTS.md §11, 27 de octubre de 2026).
/// </summary>
/// <param name="CreadoPorAdministrador">
/// Su contraseña la fijó un administrador y solo un administrador la cambia. La pantalla lo
/// muestra para que quede claro de quién es cada acceso.
/// </param>
/// <param name="Empresas">Empresas de la cuenta a las que tiene acceso activo.</param>
public sealed record UsuarioDeLaCuentaDto(
    Guid UsuarioId,
    string Nombre,
    string Correo,
    bool Activo,
    bool EsUsuarioActual,
    bool CreadoPorAdministrador,
    IReadOnlyList<string> Permisos,
    bool EsTitular,
    IReadOnlyList<Guid> Empresas);

/// <summary>
/// Una empresa activa de la cuenta, para elegir a cuáles entra un usuario. Lleva sus licencias
/// porque los permisos de Notaría, Obra y Comercio Exterior solo se ofrecen donde hay módulo.
/// </summary>
public sealed record EmpresaParaAccesoDto(
    Guid Id,
    string NombreFiscal,
    string Rfc,
    LicenciasDto Licencias);

/// <param name="LimiteDeUsuarios">Cuántos usuarios activos admite la cuenta, contando al titular.</param>
/// <param name="UsuariosEnLaCuenta">Cuántos ocupan lugar hoy, en todas las empresas de la cuenta.</param>
public sealed record UsuariosDeLaCuentaDto(
    IReadOnlyList<UsuarioDeLaCuentaDto> Usuarios,
    int LimiteDeUsuarios,
    int UsuariosEnLaCuenta,
    IReadOnlyList<EmpresaParaAccesoDto> Empresas);

/// <summary>
/// Alta de un usuario por parte de un administrador. Sustituyó a la invitación por correo:
/// el administrador teclea la contraseña y se la comunica por fuera del sistema.
///
/// <para>
/// La contraseña la valida ASP.NET Core Identity con sus reglas de longitud y composición
///. El servidor no la devuelve nunca, ni siquiera al que la acaba de escribir.
/// </para>
/// <para>
/// <c>Empresas</c> es la excepción a «el Client nunca envía una empresa» (AGENTS.md §11, 27 de
/// octubre de 2026): no elige dónde se trabaja, dice a cuáles entrará el usuario, y el
/// servidor rechaza cualquiera que no sea una empresa activa de la cuenta del titular.
/// </para>
/// </summary>
public sealed record PeticionCrearUsuario(
    string Correo,
    string Nombre,
    string Contrasena,
    IReadOnlyList<string> Permisos,
    IReadOnlyList<Guid> Empresas);

public sealed record RespuestaCrearUsuario(string Mensaje);

/// <summary>Cambio del correo de otro usuario, hecho por un administrador.</summary>
public sealed record PeticionCambiarCorreoUsuario(string Correo);

/// <summary>
/// El administrador le pone una contraseña nueva a un usuario que él dio de alta. No pide la
/// anterior: el sentido de esto es precisamente que el usuario la olvidó, o que dejó de
/// trabajar aquí y hay que cerrarle el paso.
/// </summary>
public sealed record PeticionCambiarContrasenaUsuario(string Contrasena);

/// <summary>Permisos y empresas de un usuario. Ver <see cref="PeticionCrearUsuario"/> sobre <c>Empresas</c>.</summary>
public sealed record PeticionActualizarPermisos(IReadOnlyList<string> Permisos, IReadOnlyList<Guid> Empresas);

public sealed record PeticionCambiarActivoUsuario(bool Activo);

/// <param name="PuedeCambiarContrasena">
/// Falso para el usuario que creó un administrador: su contraseña la administra él. La
/// pantalla de perfil esconde el formulario, y el servidor lo rechaza igual — esconder un
/// botón no es proteger.
/// </param>
public sealed record PerfilDto(string Nombre, string Correo, string Tema, bool PuedeCambiarContrasena);

public sealed record PeticionActualizarPerfil(string Nombre);

public sealed record PeticionCambiarContrasena(string ContrasenaActual, string ContrasenaNueva);
