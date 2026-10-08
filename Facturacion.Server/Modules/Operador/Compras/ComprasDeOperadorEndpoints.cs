using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Server.Modules.Plataforma.Timbres;
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
        grupo.MapGet("/{id:guid}/comprobante", Comprobante)
            .RequireAuthorization(PoliticasDeOperador.VerCompras);

        // Cada resolución exige su acción concreta; el permiso anterior conserva ambas.
        grupo.MapPost("/{id:guid}/acreditar", Acreditar).RequireAuthorization(PoliticasDeOperador.SoloAcreditarCompras);
        grupo.MapPost("/{id:guid}/rechazar", Rechazar).RequireAuthorization(PoliticasDeOperador.DescartarCompras);
    }

    private static async Task<IResult> Listar(
        ServicioDeComprasDeOperador compras,
        CancellationToken ct,
        string? estado = null,
        string? texto = null,
        int pagina = 0,
        int tamano = 25)
        => Results.Ok(await compras.ListarAsync(estado, texto, pagina, Math.Clamp(tamano, 1, 100), ct));

    private static async Task<IResult> Comprobante(
        Guid id,
        ServicioDeComprobantesDeCompra comprobantes,
        HttpContext contexto,
        CancellationToken ct)
    {
        var resultado = await comprobantes.GenerarParaOperadorAsync(id, ct);

        return resultado.EsExito
            ? Results.File(resultado.Valor.Contenido, "application/pdf", resultado.Valor.Nombre)
            : resultado.Error!.AResultado(contexto);
    }

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
