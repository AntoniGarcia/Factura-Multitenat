using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Modules.Documentos.Salidas;
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
/// Consultar y descargar lo abre <c>ver_documentos</c> o cualquier permiso de Emitir. Crear,
/// editar y descartar exige el permiso del tipo de documento: la política del endpoint deja pasar
/// a quien emite alguna factura y el servicio exige la variante exacta (AGENTS.md §11).
/// </para>
/// </summary>
public static class EmisionEndpoints
{
    public static void MapEmision(this IEndpointRouteBuilder rutas)
    {
        // Por endpoint y no en el grupo: en el grupo se acumularía sobre todos sus hijos.
        var grupo = rutas.MapGroup("/api/documentos").WithTags("Documentos");

        grupo.MapPost("/borradores", CrearBorrador).RequireAuthorization(Permisos.Politicas.EmitirFacturas);
        grupo.MapGet("", Listar).RequireAuthorization(Permisos.Politicas.Documentos);
        grupo.MapGet("/{id:guid}", Obtener).RequireAuthorization(Permisos.Politicas.Documentos);
        grupo.MapGet("/{id:guid}/vista-previa.pdf", VistaPreviaPdf).RequireAuthorization(Permisos.Politicas.Documentos);
        grupo.MapGet("/{id:guid}/pdf", DescargarPdf).RequireAuthorization(Permisos.Politicas.Documentos);
        grupo.MapGet("/{id:guid}/xml", DescargarXml).RequireAuthorization(Permisos.Politicas.Documentos);
        grupo.MapGet("/{id:guid}/descarga", DescargarZip).RequireAuthorization(Permisos.Politicas.Documentos);
        grupo.MapPut("/{id:guid}", Guardar).RequireAuthorization(Permisos.Politicas.EmitirFacturas);
        grupo.MapDelete("/{id:guid}", EliminarBorrador).RequireAuthorization(Permisos.Politicas.Emitir);
        grupo.MapGet("/relacionados/resolver", ResolverRelacionado).RequireAuthorization(Permisos.Politicas.Emitir);
    }

    // Cuerpo opcional: sin él se crea una factura básica, como antes de que existieran las variantes.
    private static async Task<IResult> CrearBorrador(
        PeticionCrearBorrador? peticion, ServicioDeEmision emision, HttpContext http, CancellationToken ct)
    {
        var resultado = await emision.CrearBorradorAsync(peticion?.Variante, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(http)
            : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> Obtener(
        Guid id, ServicioDeEmision emision, HttpContext http, CancellationToken ct)
    {
        var comprobante = await emision.ObtenerAsync(id, ct);
        return comprobante is null
            ? ErrorNegocio
                .NoEncontrado("comprobante-no-encontrado", "Ese comprobante no existe.")
                .AResultado(http)
            : Results.Ok(comprobante);
    }

    private static async Task<IResult> Guardar(
        Guid id, PeticionGuardarBorrador peticion, ServicioDeEmision emision, HttpContext http, CancellationToken ct)
    {
        var resultado = await emision.GuardarAsync(id, peticion, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(http)
            : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> VistaPreviaPdf(
        Guid id, ServicioDePdfBorrador pdf, HttpContext http, CancellationToken ct)
    {
        var resultado = await pdf.GenerarAsync(id, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(http)
            : Results.File(resultado.Valor!, "application/pdf");
    }

    private static async Task<IResult> DescargarPdf(
        Guid id, ServicioDeSalidasFiscales salidas, HttpContext http, CancellationToken ct)
    {
        var resultado = await salidas.GenerarPdfAsync(id, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(http)
            : Results.File(resultado.Valor!.Contenido, resultado.Valor.TipoContenido, resultado.Valor.Nombre);
    }

    private static async Task<IResult> DescargarXml(
        Guid id, ServicioDeSalidasFiscales salidas, HttpContext http, CancellationToken ct)
    {
        var resultado = await salidas.ObtenerXmlAsync(id, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(http)
            : Results.File(resultado.Valor!.Contenido, resultado.Valor.TipoContenido, resultado.Valor.Nombre);
    }

    private static async Task<IResult> DescargarZip(
        Guid id, ServicioDeSalidasFiscales salidas, HttpContext http, CancellationToken ct)
    {
        var resultado = await salidas.GenerarZipAsync(id, ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(http)
            : Results.File(resultado.Valor!.Contenido, resultado.Valor.TipoContenido, resultado.Valor.Nombre);
    }

    private static async Task<IResult> EliminarBorrador(
        Guid id, ServicioDeEmision emision, HttpContext http, CancellationToken ct)
    {
        var resultado = await emision.EliminarBorradorAsync(id, ct);

        return resultado.EsFallo ? resultado.Error!.AResultado(http) : Results.NoContent();
    }

    /*
    // Filtro por boloque de archivos de un cliente
    private static async Task<IResult> Listar(
    ServicioDeEmision emision, HttpContext http, CancellationToken ct,
    string? texto = null, string? estatus = null,
    Guid[]? clienteIds = null, string? tipoComprobante = null,
    DateTime? desdeUtc = null, DateTime? hastaUtc = null,
    int pagina = 0, int tamano = 25, string? orden = null, bool descendente = false)
{
    if (!TryEstatus(estatus, out var estatusFiltro))
        return ErrorNegocio
            .Validacion("estatus-desconocido", $"«{estatus}» no es un estatus de comprobante.")
            .AResultado(http);

    var tamanoEfectivo = Math.Clamp(tamano, 1, 100);
    var paginaEfectiva = Math.Max(pagina, 0);

    return Results.Ok(await emision.ListarAsync(
        texto, estatusFiltro, clienteIds, tipoComprobante,
        desdeUtc, hastaUtc, paginaEfectiva, tamanoEfectivo, orden, descendente, ct));
    */
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
