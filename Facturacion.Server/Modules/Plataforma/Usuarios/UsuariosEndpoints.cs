using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Server.Modules.Plataforma.Usuarios;

/// <summary>
/// Usuarios e invitaciones de la empresa activa. Todo bajo <c>administrar_usuarios</c>
/// (PROMPT-FASES-A §8); el perfil propio vive aparte, en <see cref="PerfilEndpoints"/>, sin
/// ese permiso.
/// </summary>
public static class UsuariosEndpoints
{
    public static void MapUsuarios(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/usuarios")
            .WithTags("Usuarios")
            .RequireAuthorization(Permisos.AdministrarUsuarios);

        grupo.MapGet("/", Listar);
        grupo.MapPost("/invitar", Invitar);
        grupo.MapPost("/invitaciones/{id:guid}/reenviar", Reenviar);
        grupo.MapPost("/invitaciones/{id:guid}/revocar", Revocar);
        grupo.MapPut("/{usuarioId:guid}/permisos", ActualizarPermisos);
        grupo.MapPost("/{usuarioId:guid}/activo", CambiarActivo);

        // Sin sesión: quien acepta una invitación todavía no tiene cuenta.
        var publico = rutas.MapGroup("/api/invitaciones").WithTags("Invitaciones").AllowAnonymous();

        publico.MapGet("/{token}", ObtenerPublica);
        publico.MapPost("/aceptar", Aceptar);
    }

    private static async Task<IResult> Listar(ServicioDeInvitaciones invitaciones, CancellationToken ct)
        => Results.Ok(await invitaciones.ListarAsync(ct));

    private static async Task<IResult> Invitar(
        PeticionInvitarUsuario peticion, ServicioDeInvitaciones invitaciones, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await invitaciones.InvitarAsync(peticion, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(contexto)
            : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> Reenviar(Guid id, ServicioDeInvitaciones invitaciones, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await invitaciones.ReenviarAsync(id, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.NoContent();
    }

    private static async Task<IResult> Revocar(Guid id, ServicioDeInvitaciones invitaciones, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await invitaciones.RevocarAsync(id, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.NoContent();
    }

    private static async Task<IResult> ActualizarPermisos(
        Guid usuarioId, PeticionActualizarPermisos peticion, ServicioDeInvitaciones invitaciones,
        HttpContext contexto, CancellationToken ct)
    {
        var resultado = await invitaciones.ActualizarPermisosAsync(usuarioId, peticion.Permisos, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.NoContent();
    }

    private static async Task<IResult> CambiarActivo(
        Guid usuarioId, PeticionCambiarActivoUsuario peticion, ServicioDeInvitaciones invitaciones,
        HttpContext contexto, CancellationToken ct)
    {
        var resultado = await invitaciones.CambiarActivoAsync(usuarioId, peticion.Activo, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> ObtenerPublica(string token, ServicioDeInvitaciones invitaciones, CancellationToken ct)
    {
        var invitacion = await invitaciones.ObtenerPublicaAsync(token, ct);
        return invitacion is null ? Results.NotFound() : Results.Ok(invitacion);
    }

    private static async Task<IResult> Aceptar(
        PeticionAceptarInvitacion peticion, ServicioDeInvitaciones invitaciones, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await invitaciones.AceptarAsync(peticion, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.NoContent();
    }
}
