using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;

namespace Facturacion.Server.Modules.Documentos.Emision;

/// <summary>
/// Alta y edición de borradores de factura estándar. El timbrado en sí vive en
/// <see cref="Timbrado.TimbradoEndpoints"/>: son dos endpoints con dos preocupaciones muy
/// distintas —uno guarda cuantas veces haga falta, el otro gasta un timbre y un folio una
/// sola vez— y mezclarlos habría escondido esa diferencia.
///
/// <para>
/// Requieren <c>timbrar</c> y no un permiso propio: en la lista de permisos de ARQUITECTURA.md §4
/// no hay uno de «capturar documentos», y quien no puede timbrar tampoco tiene por qué poder
/// dejar borradores a medio armar en el sistema.
/// </para>
/// </summary>
public static class EmisionEndpoints
{
    public static void MapEmision(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/documentos")
            .WithTags("Documentos")
            .RequireAuthorization(Permisos.Timbrar);

        grupo.MapPost("/borradores", CrearBorrador);
        grupo.MapGet("", Listar);
        grupo.MapGet("/{id:guid}", Obtener);
        grupo.MapPut("/{id:guid}", Guardar);
        grupo.MapDelete("/{id:guid}", EliminarBorrador);
        grupo.MapGet("/relacionados/resolver", ResolverRelacionado);
    }

    private static async Task<IResult> CrearBorrador(ServicioDeEmision emision, CancellationToken ct)
        => Results.Ok(await emision.CrearBorradorAsync(ct));

    private static async Task<IResult> Obtener(Guid id, ServicioDeEmision emision, CancellationToken ct)
    {
        var comprobante = await emision.ObtenerAsync(id, ct);
        return comprobante is null ? Results.NotFound() : Results.Ok(comprobante);
    }

    private static async Task<IResult> Guardar(
        Guid id, PeticionGuardarBorrador peticion, ServicioDeEmision emision, HttpContext http, CancellationToken ct)
    {
        var resultado = await emision.GuardarAsync(id, peticion, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(http)
            : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> EliminarBorrador(
        Guid id, ServicioDeEmision emision, HttpContext http, CancellationToken ct)
    {
        var resultado = await emision.EliminarBorradorAsync(id, ct);

        return resultado.EsFallo ? resultado.Error!.AResultado(http) : Results.NoContent();
    }

    private static async Task<IResult> Listar(
        ServicioDeEmision emision, HttpContext http, CancellationToken ct,
        string? texto = null, string? estatus = null,
        Guid? clienteId = null, string? tipoComprobante = null,
        DateTime? desdeUtc = null, DateTime? hastaUtc = null,
        int pagina = 0, int tamano = 25, string? orden = null, bool descendente = false)
    {
        if (!TryEstatus(estatus, out var estatusFiltro))
            return ErrorNegocio
                .Validacion("estatus-desconocido", $"«{estatus}» no es un estatus de comprobante.")
                .AResultado(http);

        // Mismo tope que el resto de los listados: sin él, alguien pide un millón de renglones
        // y se lleva el histórico completo de una sentada.
        var tamanoEfectivo = Math.Clamp(tamano, 1, 100);
        var paginaEfectiva = Math.Max(pagina, 0);

        return Results.Ok(await emision.ListarAsync(
            texto, estatusFiltro, clienteId, tipoComprobante,
            desdeUtc, hastaUtc, paginaEfectiva, tamanoEfectivo, orden, descendente, ct));
    }

    /// <summary>
    /// El estatus viaja como la cadena de ARQUITECTURA.md §5 —la misma que guarda la columna—, no
    /// como el nombre del miembro del enum. El enlace automático de minimal APIs resuelve los
    /// enums por nombre de miembro y distinguiendo mayúsculas, así que <c>en_cancelacion</c>
    /// nunca enlazaría y <c>timbrado</c> tampoco: devolvía 400 antes de llegar al método.
    /// </summary>
    private static bool TryEstatus(string? valor, out EstatusComprobante? estatus)
    {
        estatus = null;

        if (string.IsNullOrWhiteSpace(valor)) return true;

        foreach (var candidato in Enum.GetValues<EstatusComprobante>())
        {
            if (candidato.ACadena() != valor) continue;

            estatus = candidato;
            return true;
        }

        return false;
    }

    private static async Task<IResult> ResolverRelacionado(
        string serie, int folio, ServicioDeEmision emision, HttpContext http, CancellationToken ct)
    {
        var resultado = await emision.ResolverRelacionadoAsync(serie, folio, ct);

        return resultado.EsFallo ? resultado.Error!.AResultado(http) : Results.Ok(resultado.Valor);
    }
}
