using System.Security.Claims;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Usuarios;

/// <summary>
/// El perfil propio: nombre, contraseña y tema. Sin <c>administrar_usuarios</c> a propósito
/// —todo usuario edita lo suyo— y siempre a partir del id del token, nunca de uno recibido
/// en el cuerpo (PROMPT-FASES-A §8).
/// </summary>
public static class PerfilEndpoints
{
    public static void MapPerfil(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/perfil").WithTags("Perfil").RequireAuthorization();

        grupo.MapGet("/", Obtener);
        grupo.MapPut("/", ActualizarNombre);
        grupo.MapPut("/tema", CambiarTema);
        grupo.MapPut("/contrasena", CambiarContrasena);
    }

    private static async Task<IResult> Obtener(AppDbContext baseDeDatos, ClaimsPrincipal usuarioActual, CancellationToken ct)
    {
        var usuario = await baseDeDatos.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == IdDe(usuarioActual), ct);

        return usuario is null
            ? Results.NotFound()
            : Results.Ok(new PerfilDto(
                usuario.Nombre, usuario.Email ?? string.Empty,
                Temas.EsValido(usuario.TemaPreferido) ? usuario.TemaPreferido! : Temas.Claro,
                PuedeCambiarContrasena: !usuario.CreadoPorAdministrador));
    }

    private static async Task<IResult> ActualizarNombre(
        PeticionActualizarPerfil peticion, AppDbContext baseDeDatos, IServicioDeBitacora bitacora,
        ClaimsPrincipal usuarioActual, CancellationToken ct)
    {
        var nombre = (peticion.Nombre ?? string.Empty).Trim();

        if (nombre.Length == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["nombre"] = ["El nombre es obligatorio."] });

        var id = IdDe(usuarioActual);
        var usuario = await baseDeDatos.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

        if (usuario is null) return Results.NotFound();

        var antes = usuario.Nombre;
        usuario.Nombre = nombre;

        bitacora.Registrar(
            EntidadesDeBitacora.Perfil, id.ToString(), AccionesDeBitacora.PerfilActualizado,
            antes: new { Nombre = antes }, despues: new { Nombre = nombre }, usuarioId: id);

        await baseDeDatos.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static async Task<IResult> CambiarTema(
        PeticionCambioTema peticion, AppDbContext baseDeDatos, ClaimsPrincipal usuarioActual, CancellationToken ct)
    {
        if (!Temas.EsValido(peticion.Tema))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tema"] = [$"El tema debe ser uno de: {string.Join(", ", Temas.Todos)}."]
            });

        var id = IdDe(usuarioActual);

        // Se filtra por el id del token, nunca por uno recibido en el cuerpo: nadie cambia
        // el tema de otro usuario.
        var filas = await baseDeDatos.Users
            .Where(u => u.Id == id)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.TemaPreferido, peticion.Tema), ct);

        return filas > 0 ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> CambiarContrasena(
        PeticionCambiarContrasena peticion, UserManager<Usuario> usuarios, AppDbContext baseDeDatos,
        IServicioDeBitacora bitacora, ClaimsPrincipal usuarioActual, CancellationToken ct)
    {
        var id = IdDe(usuarioActual);
        var usuario = await usuarios.FindByIdAsync(id.ToString());

        if (usuario is null) return Results.NotFound();

        // Se rechaza aquí y no solo escondiendo el formulario: ocultar un botón no es
        // proteger (ARQUITECTURA.md §4). Sin esta comprobación, un POST a mano bastaría para que
        // el usuario se sacara de encima al administrador que le administra el acceso.
        if (usuario.CreadoPorAdministrador)
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["contrasena"] = ["Tu contraseña la administra quien te dio de alta. Pídele que la cambie."]
                },
                title: "No puedes cambiar tu contraseña");

        var resultado = await usuarios.ChangePasswordAsync(usuario, peticion.ContrasenaActual, peticion.ContrasenaNueva);

        if (!resultado.Succeeded)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["contrasena"] = [.. resultado.Errors.Select(e => e.Description)]
            });

        // ChangePasswordAsync ya guardó el hash por su cuenta; falta solo persistir el
        // registro de bitácora que se acaba de agregar al mismo contexto.
        bitacora.Registrar(EntidadesDeBitacora.Perfil, id.ToString(), AccionesDeBitacora.ContrasenaCambiada, usuarioId: id);
        await baseDeDatos.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static Guid IdDe(ClaimsPrincipal usuario) => Guid.Parse(usuario.FindFirstValue(ClavesDeClaim.Usuario)!);
}
