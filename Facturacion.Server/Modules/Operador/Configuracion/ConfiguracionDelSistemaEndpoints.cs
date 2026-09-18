using System.Security.Claims;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Server.Modules.Operador.Configuracion;

/// <summary>
/// Configuración del sistema: correo saliente (SMTP) y nombre que aparece en los correos y
/// documentos. Solo el operador puede verla y modificarla.
/// </summary>
public static class ConfiguracionDelSistemaEndpoints
{
    public static void MapConfiguracionDelSistema(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/operador/configuracion")
            .WithTags("Operador · Configuración")
            .RequireAuthorization(PoliticasDeOperador.Operador);

        grupo.MapGet("/", Obtener).RequireAuthorization(PoliticasDeOperador.VerConfiguracion);
        grupo.MapPut("/", Guardar).RequireAuthorization(PoliticasDeOperador.AdministrarConfiguracion);
    }

    private static async Task<IResult> Obtener(
        ServicioDeConfiguracionDelSistema servicio, CancellationToken ct)
    {
        var configuracion = await servicio.ObtenerAsync(ct);
        return Results.Ok(configuracion);
    }

    private static async Task<IResult> Guardar(
        HttpContext contexto,
        ServicioDeConfiguracionDelSistema servicio,
        PeticionGuardarConfiguracionDelSistema peticion,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await servicio.GuardarAsync(operadorId, peticion, ct);

        return resultado.EsExito
            ? Results.Ok()
            : resultado.Error!.AResultado(contexto);
    }

    private static bool TryOperador(HttpContext contexto, out Guid operadorId)
        => Guid.TryParse(contexto.User.FindFirstValue(ClavesDeClaim.Operador), out operadorId);
}
