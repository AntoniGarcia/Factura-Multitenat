using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;

namespace Facturacion.Server.Modules.Documentos.Egresos;

public static class NotasCreditoEndpoints
{
    public static void MapNotasCredito(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/notas-credito").WithTags("Notas de crédito")
            .RequireAuthorization(Permisos.Politicas.EditarFacturas);
        grupo.MapGet("/origen", async (string serie, int folio, ServicioDeNotasCredito servicio, HttpContext http, CancellationToken ct) =>
        {
            var resultado = await servicio.ConsultarOrigenAsync(serie, folio, ct);
            return resultado.EsFallo ? resultado.Error!.AResultado(http) : Results.Ok(resultado.Valor);
        });
        grupo.MapGet("/{id:guid}", async (Guid id, ServicioDeNotasCredito servicio, HttpContext http, CancellationToken ct) =>
        {
            var resultado = await servicio.ObtenerAsync(id, ct);
            return resultado.EsFallo ? resultado.Error!.AResultado(http) : Results.Ok(resultado.Valor);
        });
        grupo.MapPut("/{id:guid}", async (Guid id, PeticionGuardarNotaCredito peticion, ServicioDeNotasCredito servicio, HttpContext http, CancellationToken ct) =>
        {
            var resultado = await servicio.GuardarAsync(id, peticion, ct);
            return resultado.EsFallo ? resultado.Error!.AResultado(http) : Results.Ok(resultado.Valor);
        });
    }
}
