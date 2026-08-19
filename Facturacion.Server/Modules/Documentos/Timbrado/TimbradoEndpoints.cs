using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Idempotencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;

namespace Facturacion.Server.Modules.Documentos.Timbrado;

/// <summary>
/// Timbrar es la operación con más consecuencias del sistema: gasta un timbre, consume un
/// folio que ya no se recicla y produce un documento fiscal ante el SAT.
///
/// <para><b>Por qué exige <c>Idempotency-Key</c></b></para>
/// Es lo que pide CLAUDE.md §4 para todo <c>POST</c> que cobre o timbre, y aquí protege del
/// error más caro y más fácil de cometer: un doble clic en «Generar factura», o el reintento
/// automático del navegador cuando la respuesta tarda. Sin la clave, el segundo envío entra
/// como una petición nueva y el comprobante se timbra dos veces.
///
/// <para>
/// Ojo con las dos claves, que no son la misma: la del encabezado protege <b>esta petición
/// HTTP</b> y la maneja <see cref="FiltroDeIdempotencia"/>; la del intento —la que viaja al
/// PAC— la genera el propio <see cref="ServicioDeTimbrado"/> y protege <b>el envío al PAC</b>.
/// </para>
/// </summary>
public static class TimbradoEndpoints
{
    public static void MapTimbrado(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/documentos").WithTags("Documentos");

        grupo.MapPost("/{id:guid}/timbrar", Timbrar)
            .RequireAuthorization(Permisos.Timbrar)
            .AddEndpointFilter<FiltroDeIdempotencia>();
    }

    private static async Task<IResult> Timbrar(
        Guid id, ServicioDeTimbrado timbrado, HttpContext http, CancellationToken ct)
    {
        var resultado = await timbrado.TimbrarAsync(id, ct);

        if (resultado.EsFallo) return resultado.Error!.AResultado(http);

        var comprobante = resultado.Valor;

        return Results.Ok(new RespuestaDeTimbradoDto(
            comprobante.Id,
            comprobante.Uuid,
            comprobante.Serie,
            comprobante.Folio,
            comprobante.FechaTimbradoUtc,
            comprobante.Estatus));
    }
}
