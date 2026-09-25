using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Obras;

namespace Facturacion.Server.Modules.Documentos.Obras;

public static class ObrasEndpoints
{
    public static void MapObras(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/obras/comprobantes")
            .WithTags("Constructoras")
            .RequireAuthorization(Permisos.Timbrar);
        grupo.MapGet("/{comprobanteId:guid}", Obtener);
        grupo.MapPut("/{comprobanteId:guid}", Guardar);
        grupo.MapGet("/{comprobanteId:guid}/conciliacion-fiscal", ConciliacionFiscal);
        grupo.MapGet("/{comprobanteId:guid}/estimacion.pdf", EstimacionPdf);
    }

    private static async Task<IResult> Obtener(Guid comprobanteId, ServicioDeObras obras,
        HttpContext contexto, CancellationToken ct)
    {
        var resultado = await obras.ObtenerAsync(comprobanteId, ct);
        if (resultado.EsFallo) return resultado.Error!.AResultado(contexto);
        return resultado.Valor is null ? Results.NoContent() : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> Guardar(Guid comprobanteId, PeticionGuardarDatosObra peticion,
        ServicioDeObras obras, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await obras.GuardarAsync(comprobanteId, peticion, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> ConciliacionFiscal(Guid comprobanteId, ServicioDeObras obras,
        HttpContext contexto, CancellationToken ct)
    {
        var resultado = await obras.ConciliarFiscalmenteAsync(comprobanteId, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> EstimacionPdf(Guid comprobanteId,
        ServicioDePdfEstimacionObra pdf, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await pdf.GenerarAsync(comprobanteId, ct);
        return resultado.EsFallo
            ? resultado.Error!.AResultado(contexto)
            : Results.File(resultado.Valor!, "application/pdf", $"estimacion-obra-{comprobanteId:N}.pdf");
    }
}
