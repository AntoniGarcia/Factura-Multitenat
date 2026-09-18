using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Server.Modules.Operador.Auth;

/// <summary>
/// Administración de operadores del SaaS: lista, alta, edición, permisos y baja/alta.
/// <para>
/// Todos los endpoints exigen la política de operador. Las operaciones de escritura
/// exigen además la contraseña del operador que la ejecuta (re-autenticación).
/// </para>
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
        grupo.MapPost("/", Crear).RequireAuthorization(PoliticasDeOperador.AdministrarOperadores);
        grupo.MapPut("/{id:guid}", Actualizar).RequireAuthorization(PoliticasDeOperador.AdministrarOperadores);
        grupo.MapPost("/{id:guid}/activo", CambiarActivo).RequireAuthorization(PoliticasDeOperador.AdministrarOperadores);
    }

    private static async Task<IResult> Listar(
        ServicioDeOperadores operadores,
        CancellationToken ct,
        string? texto = null,
        bool? activos = null,
        int pagina = 0,
        int tamano = 25)
    {
        var tamanoEfectivo = Math.Clamp(tamano, 1, 100);
        var paginaEfectiva = Math.Max(pagina, 0);

        return Results.Ok(await operadores.ListarAsync(texto, activos, paginaEfectiva, tamanoEfectivo, ct));
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
        PeticionCrearOperador peticion,
        ServicioDeOperadores operadores,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await operadores.CrearAsync(
            operadorId, peticion.Peticion, peticion.ContrasenaOperador, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> Actualizar(
        Guid id,
        PeticionCrearOperador peticion,
        ServicioDeOperadores operadores,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await operadores.ActualizarAsync(
            operadorId, id, peticion.Peticion, peticion.ContrasenaOperador, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> CambiarActivo(
        Guid id,
        PeticionCambiarActivoOperadorWrapper peticion,
        ServicioDeOperadores operadores,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await operadores.CambiarActivoAsync(
            operadorId, id, peticion.Peticion, peticion.ContrasenaOperador, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static bool TryOperador(HttpContext contexto, out Guid operadorId)
        => Guid.TryParse(contexto.User.FindFirstValue(ClavesDeClaim.Operador), out operadorId);
}

/// <summary>Wrapper para que el body tenga: { peticion: {...}, contrasenaOperador: "..." }</summary>
public sealed record PeticionCrearOperador(
    PeticionGuardarOperador Peticion,
    [property: Required] string ContrasenaOperador);

/// <summary>Wrapper para cambio de activo con re-autenticación.</summary>
public sealed record PeticionCambiarActivoOperadorWrapper(
    PeticionCambiarActivoOperador Peticion,
    [property: Required] string ContrasenaOperador);
