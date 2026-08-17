namespace Facturacion.Shared.Plataforma;

/// <summary>Un usuario con acceso a la empresa activa, y los permisos que tiene en ELLA.</summary>
public sealed record UsuarioDeEmpresaDto(
    Guid UsuarioId,
    string Nombre,
    string Correo,
    bool Activo,
    bool EsUsuarioActual,
    IReadOnlyList<string> Permisos);

/// <summary><c>Estado</c> es <c>"pendiente"</c>, <c>"expirada"</c> o <c>"revocada"</c>.</summary>
public sealed record InvitacionDto(
    Guid Id,
    string Correo,
    string Nombre,
    IReadOnlyList<string> Permisos,
    DateTime CreadaUtc,
    DateTime ExpiraUtc,
    string Estado);

public sealed record UsuariosDeLaEmpresaDto(
    IReadOnlyList<UsuarioDeEmpresaDto> Usuarios,
    IReadOnlyList<InvitacionDto> Invitaciones,
    int LimitePorEmpresa);

public sealed record PeticionInvitarUsuario(string Correo, string Nombre, IReadOnlyList<string> Permisos);

/// <summary>
/// <c>AccesoDirecto</c> es <c>true</c> cuando el correo ya era de un usuario de la misma
/// cuenta: no hubo invitación que aceptar, el acceso a la empresa nueva quedó listo de una vez.
/// </summary>
public sealed record RespuestaInvitar(bool AccesoDirecto, string Mensaje);

/// <summary>Lo que ve la pantalla pública de aceptar, antes de pedir la contraseña.</summary>
public sealed record InvitacionPublicaDto(string Correo, string Nombre, string NombreEmpresa);

public sealed record PeticionAceptarInvitacion(string Token, string Contrasena);

public sealed record PeticionActualizarPermisos(IReadOnlyList<string> Permisos);

public sealed record PeticionCambiarActivoUsuario(bool Activo);

public sealed record PerfilDto(string Nombre, string Correo, string Tema);

public sealed record PeticionActualizarPerfil(string Nombre);

public sealed record PeticionCambiarContrasena(string ContrasenaActual, string ContrasenaNueva);
