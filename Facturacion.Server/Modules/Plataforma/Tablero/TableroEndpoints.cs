using Facturacion.Server.Infra.Errores;

namespace Facturacion.Server.Modules.Plataforma.Tablero;

/// <summary>
/// El tablero de inicio. Una sola ruta y sin política de permiso propia: lo ve cualquiera
/// con sesión, y es el servicio el que recorta el contenido según lo que esa sesión pueda
/// ver (ver <see cref="ServicioDeTablero"/>).
/// </summary>
public static class TableroEndpoints
{
    public static void MapTablero(this IEndpointRouteBuilder rutas)
    {
        rutas.MapGet("/api/tablero", Obtener)
            .RequireAuthorization()
            .WithTags("Tablero");
    }

    private static async Task<IResult> Obtener(
        ServicioDeTablero tablero, HttpContext http, CancellationToken ct)
    {
        var resultado = await tablero.ObtenerAsync(ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(http);
    }
}
