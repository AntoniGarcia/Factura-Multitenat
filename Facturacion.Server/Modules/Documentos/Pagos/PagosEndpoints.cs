using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;

namespace Facturacion.Server.Modules.Documentos.Pagos;

/// <summary>
/// Complemento de pagos (§27 y §28 del documento funcional).
///
/// <para>
/// El timbrado no vive aquí: un CFDI de pago se timbra por <c>POST /api/documentos/{id}/timbrar</c>
/// como cualquier otro, porque es un comprobante más. Estos endpoints solo lo arman.
/// </para>
/// </summary>
public static class PagosEndpoints
{
    public static void MapPagos(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/pagos")
            .WithTags("Documentos")
            .RequireAuthorization(Permisos.EmitirPago);

        grupo.MapPost("/borradores", CrearBorrador);
        grupo.MapGet("/{id:guid}", Obtener);
        grupo.MapPut("/{id:guid}", Guardar);
        grupo.MapGet("/por-pagar", ConsultarSaldo);
        grupo.MapDelete("/{id:guid}", EliminarBorrador);
    }

    private static async Task<IResult> CrearBorrador(ServicioDePagos pagos, HusoDeEmpresa huso, CancellationToken ct)
        => Results.Ok(await ConHoraLocal(await pagos.CrearBorradorAsync(ct), huso, ct));

    private static async Task<IResult> Obtener(Guid id, ServicioDePagos pagos, HusoDeEmpresa huso, CancellationToken ct)
    {
        var pago = await pagos.ObtenerAsync(id, ct);
        return pago is null ? Results.NotFound() : Results.Ok(await ConHoraLocal(pago, huso, ct));
    }

    private static async Task<IResult> Guardar(
        Guid id, PeticionGuardarPago peticion, ServicioDePagos pagos, HusoDeEmpresa huso, HttpContext http, CancellationToken ct)
    {
        var resultado = await pagos.GuardarAsync(id, peticion, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(http)
            : Results.Ok(await ConHoraLocal(resultado.Valor, huso, ct));
    }

    private static async Task<PagoDto> ConHoraLocal(PagoDto pago, HusoDeEmpresa huso, CancellationToken ct)
    {
        var zona = await huso.ObtenerAsync(ct);
        return pago with
        {
            FechaPagoLocal = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(pago.FechaPagoUtc, DateTimeKind.Utc), zona), DateTimeKind.Unspecified),
            ZonaHoraria = zona.Id
        };
    }

    /// <summary>El botón «Consulta» de §27: qué se le debe a una factura y qué parcialidad toca.</summary>
    private static async Task<IResult> ConsultarSaldo(
        string serie, int folio, ServicioDePagos pagos, HttpContext http, CancellationToken ct)
    {
        var resultado = await pagos.ConsultarSaldoAsync(serie, folio, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(http)
            : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> EliminarBorrador(
        Guid id, ServicioDePagos pagos, HttpContext http, CancellationToken ct)
    {
        var resultado = await pagos.EliminarBorradorAsync(id, ct);
        return resultado.EsFallo ? resultado.Error!.AResultado(http) : Results.NoContent();
    }
}
