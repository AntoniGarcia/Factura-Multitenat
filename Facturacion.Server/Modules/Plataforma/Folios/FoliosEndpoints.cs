using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Server.Modules.Plataforma.Folios;

/// <summary>Administra series sin exponer una reserva de folios por HTTP. El servicio de timbrado realiza la reserva internamente.</summary>
public static class FoliosEndpoints
{
    public static void MapFolios(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/series").WithTags("Series y folios");

        // Por endpoint y no en el grupo: RequireAuthorization en el grupo se acumularía sobre
        // TODOS sus hijos, y '/activas' terminaría exigiendo también un permiso de Series.
        grupo.MapGet("/activas", ListarActivas).RequireAuthorization();

        grupo.MapGet("/", Listar).RequireAuthorization(Permisos.Politicas.Series);
        grupo.MapPost("/", Crear).RequireAuthorization(Permisos.SeriesCrear);
        grupo.MapPut("/{id:guid}", Actualizar).RequireAuthorization(Permisos.SeriesModificar);
        grupo.MapPost("/{id:guid}/activa", CambiarActiva).RequireAuthorization(Permisos.SeriesDesactivar);
        grupo.MapDelete("/{id:guid}", Eliminar).RequireAuthorization(Permisos.SeriesEliminar);
    }

    private static async Task<IResult> CambiarActiva(
        Guid id, Clientes.PeticionCambiarActivo peticion, ServicioDeSeries series, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await series.CambiarActivaAsync(id, peticion.Activo, ct);

        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> Eliminar(
        Guid id, ServicioDeSeries series, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await series.EliminarAsync(id, ct);

        return resultado.EsFallo ? resultado.Error!.AResultado(contexto) : Results.NoContent();
    }

    private static async Task<IResult> Listar(ServicioDeSeries series, CancellationToken ct)
        => Results.Ok(await series.ListarAsync(ct));

    private static async Task<IResult> ListarActivas(
        ServicioDeSeries series, CancellationToken ct, string tipoComprobante = "I")
        => Results.Ok(await series.ListarActivasAsync(tipoComprobante, ct));

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
