using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Shared.Operador;

namespace Facturacion.Server.Modules.Operador.Compras;

/// <summary>
/// Las compras de timbres desde el panel del proveedor: consultarlas todas, acreditar el pago
/// y descartar las que no se van a cobrar.
/// </summary>
public static class ComprasDeOperadorEndpoints
{
    public static void MapComprasDeOperador(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/operador/compras")
            .WithTags("Operador · Compras")
            .RequireAuthorization(PoliticasDeOperador.Operador);

        grupo.MapGet("/", Listar).RequireAuthorization(PoliticasDeOperador.VerCompras);

        // Acreditar y rechazar mueven dinero/saldo → permiso AcreditarCompras
        grupo.MapPost("/{id:guid}/acreditar", Acreditar).RequireAuthorization(PoliticasDeOperador.AcreditarCompras);
        grupo.MapPost("/{id:guid}/rechazar", Rechazar).RequireAuthorization(PoliticasDeOperador.AcreditarCompras);
    }

    private static async Task<IResult> Listar(
        ServicioDeComprasDeOperador compras,
        CancellationToken ct,
        string? estado = null,
        string? texto = null,
        int pagina = 0,
        int tamano = 25)
        => Results.Ok(await compras.ListarAsync(estado, texto, pagina, Math.Clamp(tamano, 1, 100), ct));

    private static async Task<IResult> Acreditar(
        Guid id,
        ServicioDeComprasDeOperador compras,
        HttpContext contexto,
        CancellationToken ct)
    {
        var resultado = await compras.AcreditarAsync(id, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> Rechazar(
        Guid id,
        PeticionRechazarCompra peticion,
        ServicioDeComprasDeOperador compras,
        HttpContext contexto,
        CancellationToken ct)
    {
        var resultado = await compras.RechazarAsync(id, peticion.Motivo, ct);

        return resultado.EsExito
            ? Results.NoContent()
            : resultado.Error!.AResultado(contexto);
    }
}
