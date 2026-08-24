using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Server.Modules.Plataforma.Usuarios;

/// <summary>
/// Usuarios de la empresa activa. Todo bajo <c>administrar_usuarios</c>; el perfil propio
/// vive aparte, en <see cref="PerfilEndpoints"/>, sin ese permiso.
///
/// <para>
/// Ya no hay grupo anónimo: el de <c>/api/invitaciones</c> existía para que alguien sin
/// cuenta pudiera aceptar una invitación, y ese camino se eliminó.
/// </para>
/// </summary>
public static class UsuariosEndpoints
{
    public static void MapUsuarios(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/usuarios")
            .WithTags("Usuarios")
            .RequireAuthorization(Permisos.AdministrarUsuarios);

        grupo.MapGet("/", Listar);
        grupo.MapPost("/", Crear);
        grupo.MapPut("/{usuarioId:guid}/correo", CambiarCorreo);
        grupo.MapPut("/{usuarioId:guid}/contrasena", CambiarContrasena);
        grupo.MapPut("/{usuarioId:guid}/permisos", ActualizarPermisos);
        grupo.MapPost("/{usuarioId:guid}/activo", CambiarActivo);
    }

    private static async Task<IResult> Listar(ServicioDeUsuarios usuarios, CancellationToken ct)
        => Results.Ok(await usuarios.ListarAsync(ct));

    private static async Task<IResult> Crear(
        PeticionCrearUsuario peticion, ServicioDeUsuarios usuarios, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await usuarios.CrearAsync(peticion, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(contexto)
            : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> CambiarCorreo(
        Guid usuarioId, PeticionCambiarCorreoUsuario peticion, ServicioDeUsuarios usuarios,
        HttpContext contexto, CancellationToken ct)
    {
        var resultado = await usuarios.CambiarCorreoAsync(usuarioId, peticion.Correo, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.NoContent();
    }

    private static async Task<IResult> CambiarContrasena(
        Guid usuarioId, PeticionCambiarContrasenaUsuario peticion, ServicioDeUsuarios usuarios,
        HttpContext contexto, CancellationToken ct)
    {
        var resultado = await usuarios.CambiarContrasenaAsync(usuarioId, peticion.Contrasena, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.NoContent();
    }

    private static async Task<IResult> ActualizarPermisos(
        Guid usuarioId, PeticionActualizarPermisos peticion, ServicioDeUsuarios usuarios,
        HttpContext contexto, CancellationToken ct)
    {
        var resultado = await usuarios.ActualizarPermisosAsync(usuarioId, peticion.Permisos, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.NoContent();
    }

    private static async Task<IResult> CambiarActivo(
        Guid usuarioId, PeticionCambiarActivoUsuario peticion, ServicioDeUsuarios usuarios,
        HttpContext contexto, CancellationToken ct)
    {
        var resultado = await usuarios.CambiarActivoAsync(usuarioId, peticion.Activo, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.Ok(resultado.Valor);
    }
}
