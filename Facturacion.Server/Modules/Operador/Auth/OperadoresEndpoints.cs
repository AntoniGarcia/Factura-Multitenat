using System.Security.Claims;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
using Facturacion.Server.Modules.Plataforma.Auth;

namespace Facturacion.Server.Modules.Operador.Auth;

/// <summary>
/// Administración de operadores del SaaS: lista, alta, edición, permisos y baja/alta.
/// </summary>
public static class OperadoresEndpoints
{
    public static void MapOperadores(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/operador/operadores")
            .WithTags("Operador · Operadores")
            .RequireAuthorization(PoliticasDeOperador.Operador);

        grupo.MapGet("/", Listar).RequireAuthorization(PoliticasDeOperador.VerOperadores);
        grupo.MapGet("/{id:guid}", Obtener).RequireAuthorization(PoliticasDeOperador.VerOperadores);
        grupo.MapGet("/{id:guid}/actividad", async (Guid id, int? pagina, ServicioDeOperadores servicio, CancellationToken ct) =>
        {
            if (await servicio.ObtenerAsync(id, ct) is null) return Results.NotFound();
            return Results.Ok(await servicio.ActividadAsync(id, Math.Clamp(pagina ?? 0, 0, 100_000), ct));
        }).RequireAuthorization(PoliticasDeOperador.VerActividadOperadores);
        grupo.MapDelete("/{id:guid}/actividad/sesiones", async (Guid id, ServicioDeOperadores operadores,
            HttpContext contexto, CancellationToken ct) =>
        {
            if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();
            var resultado = await operadores.LimpiarHistorialDeSesionesAsync(operadorId, id, ct);
            return resultado.EsExito ? Results.Ok(resultado.Valor) : resultado.Error!.AResultado(contexto);
        }).RequireAuthorization(PoliticasDeOperador.VerActividadOperadores, PermisosDePanel.LimpiarActividadOperadores);
        grupo.MapPost("/", Crear).RequireAuthorization(PoliticasDeOperador.CrearOperadores);
        grupo.MapPut("/{id:guid}", Actualizar).RequireAuthorization(PoliticasDeOperador.EditarOperadores);
        grupo.MapPost("/{id:guid}/activo", CambiarActivo);
        grupo.MapPost("/{id:guid}/eliminar", Eliminar).RequireAuthorization(PoliticasDeOperador.EliminarOperadores);
    }

    private static async Task<IResult> Listar(
        ServicioDeOperadores operadores,
        CancellationToken ct,
        string? texto = null,
        bool? activos = null,
        bool eliminados = false,
        int pagina = 0,
        int tamano = 25)
    {
        var tamanoEfectivo = Math.Clamp(tamano, 1, 100);
        var paginaEfectiva = Math.Max(pagina, 0);

        return Results.Ok(await operadores.ListarAsync(texto, activos, eliminados, paginaEfectiva, tamanoEfectivo, ct));
    }

    private static async Task<IResult> Eliminar(
        Guid id, ServicioDeOperadores operadores,
        HttpContext contexto, CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();
        var resultado = await operadores.EliminarAsync(operadorId, id, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.NoContent();
    }

    private static async Task<IResult> Obtener(
        Guid id,
        ServicioDeOperadores operadores,
        CancellationToken ct)
    {
        var operador = await operadores.ObtenerAsync(id, ct);

        return operador is null ? Results.NotFound() : Results.Ok(operador);
    }

    private static async Task<IResult> Crear(
        PeticionGuardarOperador peticion,
        ServicioDeOperadores operadores,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await operadores.CrearAsync(
            operadorId, peticion, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> Actualizar(
        Guid id,
        PeticionGuardarOperador peticion,
        ServicioDeOperadores operadores,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await operadores.ActualizarAsync(
            operadorId, id, peticion, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> CambiarActivo(
        Guid id,
        PeticionCambiarActivoOperador peticion,
        ServicioDeOperadores operadores,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        if (!PermisosDePanel.Autoriza(contexto.User.FindAll(ClavesDeClaim.Permiso).Select(c => c.Value),
                peticion.Activo ? PermisosDePanel.ReactivarOperadores : PermisosDePanel.DesactivarOperadores))
            return Results.Forbid(authenticationSchemes: [EsquemasDeAutenticacion.Operador]);

        var resultado = await operadores.CambiarActivoAsync(
            operadorId, id, peticion, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static bool TryOperador(HttpContext contexto, out Guid operadorId)
        => Guid.TryParse(contexto.User.FindFirstValue(ClavesDeClaim.Operador), out operadorId);
}
