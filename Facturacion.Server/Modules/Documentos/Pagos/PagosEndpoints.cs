using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;

namespace Facturacion.Server.Modules.Documentos.Pagos;

/// <summary>Construye el complemento de pagos. El timbrado se realiza mediante el endpoint común de documentos.</summary>
public static class PagosEndpoints
{
    public static void MapPagos(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/pagos")
            .WithTags("Documentos")
            .RequireAuthorization(Permisos.Politicas.Pagos);

        // Se suman a la del grupo. Guardar decide en el servicio: Crear basta para el borrador propio.
        grupo.MapPost("/borradores", CrearBorrador).RequireAuthorization(Permisos.PagoCrear);
        grupo.MapGet("/{id:guid}", Obtener);
        grupo.MapPut("/{id:guid}", Guardar);
        grupo.MapGet("/por-pagar", ConsultarSaldo);
        grupo.MapDelete("/{id:guid}", EliminarBorrador).RequireAuthorization(Permisos.PagoEliminar);
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
