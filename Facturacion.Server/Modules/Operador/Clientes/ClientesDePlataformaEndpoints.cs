using System.Security.Claims;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Server.Modules.Operador.Compras;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Server.Modules.Operador.Clientes;

/// <summary>
/// Las cuentas contratantes desde el panel del proveedor.
/// <para>
/// Las da de baja alta/reactivar una empresa con su motivo, y cambiar su dato de
/// contacto. Desactivar una cuenta completa sigue fuera de alcance: deja fuera a todos sus
/// usuarios y sus empresas de golpe, y merece su propio diseño.
/// </para>
/// </summary>
public static class ClientesDePlataformaEndpoints
{
    public static void MapClientesDePlataforma(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/operador/cuentas")
            .WithTags("Operador · Clientes")
            .RequireAuthorization(PoliticasDeOperador.Operador);

        grupo.MapGet("/", Listar);
        grupo.MapGet("/{id:guid}", Obtener);
        grupo.MapGet("/{id:guid}/empresas/{empresaId:guid}", ObtenerEmpresa);

        grupo.MapPost("/{id:guid}/empresas/{empresaId:guid}/activo", CambiarActivoEmpresa)
            .RequireAuthorization(PoliticasDeOperador.ConfigurarEmpresa);
        grupo.MapPost("/{id:guid}/correo-contacto", CambiarCorreoDeContacto)
            .RequireAuthorization(PoliticasDeOperador.ConfigurarEmpresa);
        grupo.MapPost("/{id:guid}/empresas/{empresaId:guid}/timbres", AsignarTimbres)
            .RequireAuthorization(PoliticasDeOperador.ComprarTimbres);
    }

    private static async Task<IResult> Listar(
        ServicioDeClientesDePlataforma clientes,
        CancellationToken ct,
        string? texto = null,
        int pagina = 0,
        int tamano = 25)
        => Results.Ok(await clientes.ListarAsync(texto, pagina, Math.Clamp(tamano, 1, 100), ct));

    private static async Task<IResult> Obtener(
        Guid id, ServicioDeClientesDePlataforma clientes, CancellationToken ct)
    {
        var cuenta = await clientes.ObtenerAsync(id, ct);

        return cuenta is null ? Results.NotFound() : Results.Ok(cuenta);
    }

    private static async Task<IResult> ObtenerEmpresa(
        Guid id, Guid empresaId, ServicioDeClientesDePlataforma clientes, CancellationToken ct, string? texto = null)
    {
        var empresa = await clientes.ObtenerEmpresaAsync(id, empresaId, texto, ct);

        return empresa is null ? Results.NotFound() : Results.Ok(empresa);
    }

    private static async Task<IResult> CambiarActivoEmpresa(
        Guid id,
        Guid empresaId,
        PeticionCambiarActivoDeEmpresa peticion,
        ServicioDeClientesDePlataforma clientes,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await clientes.CambiarActivoEmpresaAsync(operadorId, id, empresaId, peticion, ct);

        return resultado.EsExito
            ? Results.NoContent()
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> CambiarCorreoDeContacto(
        Guid id,
        PeticionCambiarCorreoDeContactoDeCuenta peticion,
        ServicioDeClientesDePlataforma clientes,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await clientes.CambiarCorreoDeContactoDeCuentaAsync(operadorId, id, peticion, ct);

        return resultado.EsExito
            ? Results.NoContent()
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> AsignarTimbres(
        Guid id,
        Guid empresaId,
        PeticionAsignarTimbres peticion,
        ServicioDeComprasDeOperador compras,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await compras.AsignarTimbresAsync(operadorId, id, empresaId, peticion, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static bool TryOperador(HttpContext contexto, out Guid operadorId)
        => Guid.TryParse(contexto.User.FindFirstValue(ClavesDeClaim.Operador), out operadorId);
}
