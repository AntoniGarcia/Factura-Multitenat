using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Idempotencia;
using Facturacion.Server.Modules.Documentos.Pac;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;

namespace Facturacion.Server.Modules.Documentos.Timbrado;

/// <summary>El encabezado Idempotency-Key evita peticiones duplicadas. La clave enviada al PAC es independiente y la genera el servicio de timbrado.</summary>
public static class TimbradoEndpoints
{
    public static void MapTimbrado(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/documentos").WithTags("Documentos");

        // Solo exige sesión: dice si el timbrado de la plataforma está disponible, sin datos
        // de ninguna empresa, y Configuración lo muestra a quien no necesariamente timbra.
        grupo.MapGet("/integracion-fiscal", ObtenerEstadoDeIntegracion)
            .RequireAuthorization();

        grupo.MapPost("/{id:guid}/timbrar", Timbrar)
            // Cualquier permiso de emisión entra; el servicio exige el del tipo de documento.
            .RequireAuthorization(Permisos.Politicas.Emitir)
            .AddEndpointFilter<FiltroDeIdempotencia>();
    }

    private static IResult ObtenerEstadoDeIntegracion(IServiceProvider servicios)
    {
        var pac = servicios.GetService<IProveedorPac>();
        return Results.Ok(new EstadoDeIntegracionFiscalDto(pac is not null, pac is Dobles.DobleProveedorPac));
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
