using Facturacion.Server.Modules.Operador.Auth;

namespace Facturacion.Server.Modules.Operador.Clientes;

/// <summary>
/// Las cuentas contratantes desde el panel del proveedor.
/// <para>
/// <b>Solo lectura en esta fase.</b> Desactivar una cuenta deja fuera a todos sus usuarios y
/// a todas sus empresas de golpe; eso merece su propio diseño —qué pasa con los comprobantes
/// en curso, cómo se avisa— y no se resuelve con un botón añadido de paso.
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
}
