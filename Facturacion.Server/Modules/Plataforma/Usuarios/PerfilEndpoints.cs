using System.Security.Claims;
using Facturacion.Server.Data;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Usuarios;

/// <summary>
/// El único dato de perfil que existe todavía: el tema. El resto —cambio de contraseña,
/// nombre— es la fase 8.
/// </summary>
public static class PerfilEndpoints
{
    public static void MapPerfil(this IEndpointRouteBuilder rutas)
    {
        rutas.MapPut("/api/perfil/tema", CambiarTema).RequireAuthorization();
    }

    private static async Task<IResult> CambiarTema(
        PeticionCambioTema peticion, AppDbContext baseDeDatos, ClaimsPrincipal usuarioActual, CancellationToken ct)
    {
        if (!Temas.EsValido(peticion.Tema))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tema"] = [$"El tema debe ser uno de: {string.Join(", ", Temas.Todos)}."]
            });

        var id = Guid.Parse(usuarioActual.FindFirstValue(ClavesDeClaim.Usuario)!);

        // Se filtra por el id del token, nunca por uno recibido en el cuerpo: nadie cambia
        // el tema de otro usuario.
        var filas = await baseDeDatos.Users
            .Where(u => u.Id == id)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.TemaPreferido, peticion.Tema), ct);

        return filas > 0 ? Results.NoContent() : Results.NotFound();
    }
}
