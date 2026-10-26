using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Operador.Auth;

namespace Facturacion.Server.Modules.Operador.Catalogos;

public static class CatalogosDeOperadorEndpoints
{
    public static void MapCatalogosDeOperador(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/operador/catalogos-sat").WithTags("Operador · Catálogos del SAT")
            .RequireAuthorization(PoliticasDeOperador.Operador, PoliticasDeOperador.VerCatalogosSat);
        grupo.MapGet("/", async (ServicioDeCatalogosDeOperador servicio, HttpContext contexto, CancellationToken ct) =>
        {
            contexto.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await servicio.ListarAsync(ct));
        });
        grupo.MapGet("/{catalogo}", async (string catalogo, ServicioDeCatalogosDeOperador servicio, HttpContext contexto,
            CancellationToken ct, string? texto = null, bool? vigentes = null, int pagina = 0, int tamano = 24) =>
        {
            contexto.Response.Headers.CacheControl = "no-store";
            var resultado = await servicio.ConsultarAsync(catalogo, texto, vigentes, pagina, tamano, ct);
            return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.Ok(resultado.Valor);
        });
    }
}
