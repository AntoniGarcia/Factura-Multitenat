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
        var grupo = rutas.MapGroup("/api/series").WithTags("Series y folios");

        // Por endpoint y no en el grupo: RequireAuthorization en el grupo se acumularía sobre
        // TODOS sus hijos, y '/activas' terminaría exigiendo también 'configurar_empresa'.
        grupo.MapGet("/activas", ListarActivas).RequireAuthorization();

        grupo.MapGet("/", Listar).RequireAuthorization(Permisos.ConfigurarEmpresa);
        grupo.MapPost("/", Crear).RequireAuthorization(Permisos.ConfigurarEmpresa);
        grupo.MapPut("/{id:guid}", Actualizar).RequireAuthorization(Permisos.ConfigurarEmpresa);
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
