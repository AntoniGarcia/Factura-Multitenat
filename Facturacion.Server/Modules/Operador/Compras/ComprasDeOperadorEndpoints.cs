using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Shared.Operador;

namespace Facturacion.Server.Modules.Operador.Compras;

/// <summary>
/// Las compras de timbres desde el panel del proveedor: consultarlas todas, acreditar el pago
/// y descartar las que no se van a cobrar.
/// </summary>
public static class ComprasDeOperadorEndpoints
{
    public static void MapComprasDeOperador(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/operador/compras")
            .WithTags("Operador · Compras")
            .RequireAuthorization(PoliticasDeOperador.Operador);

        grupo.MapGet("/", Listar);

        // ── Por qué este endpoint NO lleva FiltroDeIdempotencia ──────────────────────────
        //
        // Crea saldo, así que debería llevarlo. No lo lleva porque no puede: ese filtro
        // registra la clave en ClavesIdempotencia, que es una entidad de empresa —EmpresaId
        // obligatorio, bajo el filtro global y bajo el interceptor de sellado— y el operador
        // no tiene empresa activa. Intentarlo revienta con "se pidió la empresa activa en una
        // petición que no la tiene".
        //
        // Hacerlo compatible exigiría volver EmpresaId opcional en esa tabla y tocar el filtro
        // global y el interceptor: infraestructura que hoy protege la compra del inquilino y
        // tiene pruebas. No vale el riesgo, porque la propiedad que de verdad importa —que
        // acreditar dos veces no entregue timbres dos veces— ya está garantizada aguas abajo:
        // dbo.AcreditarCompra solo actúa si la compra sigue pendiente, dentro de una
        // transacción con bloqueo de renglón. El segundo intento recibe un 409 y la bolsa no
        // se mueve.
        //
        // Lo único que se pierde es que un reintento de red conteste 200 en vez de 409. Si
        // algún día se quiere eso, el camino es hacer opcional la tenencia de la clave, no
        // añadir el filtro aquí sin más.
        grupo.MapPost("/{id:guid}/acreditar", Acreditar);

        grupo.MapPost("/{id:guid}/rechazar", Rechazar);
    }

    private static async Task<IResult> Listar(
        ServicioDeComprasDeOperador compras,
        CancellationToken ct,
        string? estado = null,
        string? texto = null,
        int pagina = 0,
        int tamano = 25)
        => Results.Ok(await compras.ListarAsync(estado, texto, pagina, Math.Clamp(tamano, 1, 100), ct));

    private static async Task<IResult> Acreditar(
        Guid id,
        ServicioDeComprasDeOperador compras,
        HttpContext contexto,
        CancellationToken ct)
    {
        var resultado = await compras.AcreditarAsync(id, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> Rechazar(
        Guid id,
        PeticionRechazarCompra peticion,
        ServicioDeComprasDeOperador compras,
        HttpContext contexto,
        CancellationToken ct)
    {
        var resultado = await compras.RechazarAsync(id, peticion.Motivo, ct);

        return resultado.EsExito
            ? Results.NoContent()
            : resultado.Error!.AResultado(contexto);
    }
}
