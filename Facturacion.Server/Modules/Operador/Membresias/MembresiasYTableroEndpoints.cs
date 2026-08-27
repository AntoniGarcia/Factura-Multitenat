using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Server.Modules.Operador.Tablero;
using Facturacion.Shared.Operador;

namespace Facturacion.Server.Modules.Operador.Membresias;

/// <summary>Suscripciones de las cuentas y las cifras del negocio.</summary>
public static class MembresiasYTableroEndpoints
{
    public static void MapMembresiasYTablero(this IEndpointRouteBuilder rutas)
    {
        var membresias = rutas.MapGroup("/api/operador/membresias")
            .WithTags("Operador · Membresías")
            .RequireAuthorization(PoliticasDeOperador.Operador);

        membresias.MapGet("/", Listar);
        membresias.MapPost("/", Registrar);
        membresias.MapPost("/{id:guid}/cancelar", Cancelar);

        rutas.MapGet("/api/operador/tablero", Tablero)
            .WithTags("Operador · Tablero")
            .RequireAuthorization(PoliticasDeOperador.Operador);
    }

    private static async Task<IResult> Listar(
        Guid cuentaId, ServicioDeMembresias membresias, CancellationToken ct)
        => Results.Ok(await membresias.ListarAsync(cuentaId, ct));

    private static async Task<IResult> Registrar(
        PeticionRegistrarMembresia peticion,
        ServicioDeMembresias membresias,
        HttpContext contexto,
        CancellationToken ct)
    {
        var resultado = await membresias.RegistrarAsync(peticion, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> Cancelar(
        Guid id, ServicioDeMembresias membresias, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await membresias.CancelarAsync(id, ct);

        return resultado.EsExito
            ? Results.NoContent()
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> Tablero(
        ServicioDeTableroDeOperador tablero, CancellationToken ct)
        => Results.Ok(await tablero.ObtenerAsync(ct));
}
