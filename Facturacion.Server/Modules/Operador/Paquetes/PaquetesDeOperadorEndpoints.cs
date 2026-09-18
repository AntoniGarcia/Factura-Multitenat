using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Shared.Operador;

namespace Facturacion.Server.Modules.Operador.Paquetes;

/// <summary>
/// El catálogo de paquetes visto desde el panel del proveedor.
/// <para>
/// Lectura: permiso <c>ver_paquetes</c>. Escritura: permiso <c>administrar_paquetes</c>.
/// Nótese que el inquilino tiene su propio <c>GET /api/timbres/paquetes</c>, que solo devuelve
/// los activos y sin el orden ni el estado: son dos vistas distintas del mismo catálogo.
/// </para>
/// </summary>
public static class PaquetesDeOperadorEndpoints
{
    public static void MapPaquetesDeOperador(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/operador/paquetes")
            .WithTags("Operador · Paquetes")
            .RequireAuthorization(PoliticasDeOperador.Operador);

        grupo.MapGet("/", Listar).RequireAuthorization(PoliticasDeOperador.VerPaquetes);
        grupo.MapGet("/{id:guid}", Obtener).RequireAuthorization(PoliticasDeOperador.VerPaquetes);
        grupo.MapPost("/", Crear).RequireAuthorization(PoliticasDeOperador.AdministrarPaquetes);
        grupo.MapPut("/{id:guid}", Actualizar).RequireAuthorization(PoliticasDeOperador.AdministrarPaquetes);
        grupo.MapPost("/{id:guid}/activo", CambiarActivo).RequireAuthorization(PoliticasDeOperador.AdministrarPaquetes);
    }

    private static async Task<IResult> Listar(
        ServicioDePaquetesDeOperador paquetes, CancellationToken ct)
        => Results.Ok(await paquetes.ListarAsync(ct));

    private static async Task<IResult> Obtener(
        Guid id, ServicioDePaquetesDeOperador paquetes, CancellationToken ct)
    {
        var paquete = await paquetes.ObtenerAsync(id, ct);

        return paquete is null ? Results.NotFound() : Results.Ok(paquete);
    }

    private static async Task<IResult> Crear(
        PeticionGuardarPaquete peticion,
        ServicioDePaquetesDeOperador paquetes,
        HttpContext contexto,
        CancellationToken ct)
    {
        var resultado = await paquetes.CrearAsync(peticion, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> Actualizar(
        Guid id,
        PeticionGuardarPaquete peticion,
        ServicioDePaquetesDeOperador paquetes,
        HttpContext contexto,
        CancellationToken ct)
    {
        var resultado = await paquetes.ActualizarAsync(id, peticion, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> CambiarActivo(
        Guid id,
        PeticionCambioDeActivo peticion,
        ServicioDePaquetesDeOperador paquetes,
        HttpContext contexto,
        CancellationToken ct)
    {
        var resultado = await paquetes.CambiarActivoAsync(id, peticion.Activo, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }
}

/// <summary>Poner o quitar un paquete de la venta.</summary>
public sealed record PeticionCambioDeActivo(bool Activo);
