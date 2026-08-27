using System.Security.Claims;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Server.Modules.Operador.Clientes;

/// <summary>
/// Soporte sobre los usuarios de las cuentas: restablecer contraseñas y dar de baja accesos.
/// <para>
/// Son las dos operaciones del panel con más alcance sobre datos ajenos, así que van en su
/// propio grupo y las dos exigen la contraseña del operador dentro del cuerpo.
/// </para>
/// </summary>
public static class UsuariosDePlataformaEndpoints
{
    public static void MapUsuariosDePlataforma(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/operador/usuarios")
            .WithTags("Operador · Usuarios")
            .RequireAuthorization(PoliticasDeOperador.Operador);

        grupo.MapGet("/", Listar);
        grupo.MapPut("/{id:guid}/contrasena", RestablecerContrasena);
        grupo.MapPost("/{id:guid}/activo", CambiarActivo);
    }

    private static async Task<IResult> Listar(
        ServicioDeUsuariosDePlataforma usuarios,
        CancellationToken ct,
        string? texto = null,
        Guid? cuentaId = null,
        int pagina = 0,
        int tamano = 25)
        => Results.Ok(await usuarios.ListarAsync(texto, cuentaId, pagina, Math.Clamp(tamano, 1, 100), ct));

    private static async Task<IResult> RestablecerContrasena(
        Guid id,
        PeticionRestablecerContrasenaDeUsuario peticion,
        ServicioDeUsuariosDePlataforma usuarios,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await usuarios.RestablecerContrasenaAsync(operadorId, id, peticion, ct);

        return resultado.EsExito
            ? Results.NoContent()
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> CambiarActivo(
        Guid id,
        PeticionCambiarActivoDeUsuario peticion,
        ServicioDeUsuariosDePlataforma usuarios,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await usuarios.CambiarActivoAsync(operadorId, id, peticion, ct);

        return resultado.EsExito
            ? Results.NoContent()
            : resultado.Error!.AResultado(contexto);
    }

    private static bool TryOperador(HttpContext contexto, out Guid operadorId)
        => Guid.TryParse(contexto.User.FindFirstValue(ClavesDeClaim.Operador), out operadorId);
}
