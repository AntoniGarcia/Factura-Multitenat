using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Server.Modules.Plataforma.Folios;

/// <summary>
/// Administración de series. <b>No hay endpoint para reservar folio</b>: la reserva la hace
/// la mitad B por <see cref="Facturacion.Shared.Contratos.IServicioFolios"/> dentro del
/// timbrado, y exponerla por HTTP permitiría quemar la numeración de una empresa desde fuera.
/// </summary>
public static class FoliosEndpoints
{
    public static void MapFolios(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/series")
            .WithTags("Series y folios")
            .RequireAuthorization(Permisos.ConfigurarEmpresa);

        grupo.MapGet("/", Listar);
        grupo.MapPost("/", Crear);
        grupo.MapPut("/{id:guid}", Actualizar);
    }

    private static async Task<IResult> Listar(ServicioDeSeries series, CancellationToken ct)
        => Results.Ok(await series.ListarAsync(ct));

    private static async Task<IResult> Crear(
        PeticionGuardarSerie peticion, ServicioDeSeries series, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await series.CrearAsync(peticion, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(contexto)
            : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> Actualizar(
        Guid id, PeticionGuardarSerie peticion, ServicioDeSeries series, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await series.ActualizarAsync(id, peticion, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(contexto)
            : Results.Ok(resultado.Valor);
    }
}
