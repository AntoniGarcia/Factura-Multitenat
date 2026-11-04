using System.Security.Claims;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Server.Modules.Operador.Compras;

public static class FacturasDeComprasEndpoints
{
    public static void MapFacturasDeCompras(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/operador/compras").WithTags("Operador · Facturas de compras")
            .RequireAuthorization(PoliticasDeOperador.Operador);
        grupo.MapGet("/{id:guid}/factura", async (Guid id, ServicioDeFacturasDeCompras servicio,
            HttpContext contexto, CancellationToken ct) =>
        {
            var r = await servicio.ObtenerAsync(id, ct);
            return r.EsExito ? Results.Ok(r.Valor) : r.Error!.AResultado(contexto);
        }).RequireAuthorization(PoliticasDeOperador.VerCompras);
        grupo.MapPut("/{id:guid}/factura", async (Guid id, PeticionGuardarFacturaCompra p,
            ServicioDeFacturasDeCompras servicio, HttpContext contexto, CancellationToken ct) =>
        {
            if (!Guid.TryParse(contexto.User.FindFirstValue(ClavesDeClaim.Operador), out var operadorId))
                return Results.Unauthorized();
            var r = await servicio.GuardarAsync(operadorId, id, p, ct);
            return r.EsExito ? Results.Ok(r.Valor) : r.Error!.AResultado(contexto);
        }).RequireAuthorization(PoliticasDeOperador.FacturarCompras);
        grupo.MapGet("/facturas/catalogos/{catalogo}", async (string catalogo, string? texto, string? clave,
            ServicioDeFacturasDeCompras servicio, CancellationToken ct) =>
                Results.Ok(await servicio.OpcionesAsync(catalogo, texto, clave, ct)))
            .RequireAuthorization(PoliticasDeOperador.VerCompras);
    }
}
