using Facturacion.Shared.Comun;

namespace Facturacion.Shared.Documentos;

/// <summary>
/// Un comprobante para el formulario de emisión: cabecera, conceptos, relacionados e
/// información global. Sirve tanto para un borrador como para uno ya timbrado —en ese caso
/// llega de solo lectura— porque es la misma entidad la que pasa por los dos estados
/// (CLAUDE.md §5, inmutabilidad: se congela, no se duplica en otra forma).
/// </summary>
/// <param name="SerieId">
/// La serie elegida, que no es lo mismo que <paramref name="Serie"/>: el prefijo se copia al
/// timbrar y hasta entonces va nulo, así que sin este identificador el formulario no tiene de
/// dónde recuperar lo que el borrador traía guardado.
/// </param>
public sealed record ComprobanteDto(
    Guid Id,
    string Estatus,
    string TipoDeComprobante,
    Guid? SerieId,
    string? Serie,
    int? Folio,
    string Moneda,
    decimal? TipoCambio,
    string? FormaPago,
    string? MetodoPago,
    string Exportacion,
    string? CondicionesDePago,
    string? Observaciones,
    Guid? ClienteId,
    string? ReceptorRfc,
    string? ReceptorNombre,
    string? ReceptorRegimenFiscal,
    string? ReceptorDomicilioFiscal,
    string? ReceptorUsoCfdi,
    string? GlobalPeriodicidad,
    string? GlobalMeses,
    int? GlobalAnio,
    decimal SubTotal,
    decimal Descuento,
    decimal TotalImpuestosTrasladados,
    decimal TotalImpuestosRetenidos,
    decimal Total,
    Guid? Uuid,
    DateTime? FechaTimbradoUtc,
    IReadOnlyList<ConceptoDto> Conceptos,
    IReadOnlyList<ComprobanteRelacionadoDto> Relacionados);

/// <param name="ProductoId">Producto de catálogo del que se resolvió, si vino de ahí.</param>
/// <param name="Impuestos">
/// Los que trae el producto en el momento de agregarlo. Quedan editables en el renglón: el
/// motor de B1 recalcula sobre lo que haya aquí, no sobre lo que el producto tenga hoy.
/// </param>
public sealed record ConceptoDto(
    Guid? Id,
    int Orden,
    Guid? ProductoId,
    string ClaveProdServ,
    string ClaveUnidad,
    string? UnidadTexto,
    string? NoIdentificacion,
    string Descripcion,
    decimal Cantidad,
    decimal ValorUnitario,
    decimal Importe,
    decimal Descuento,
    string ObjetoImp,
    IReadOnlyList<ImpuestoDeConceptoDto> Impuestos);

public sealed record ImpuestoDeConceptoDto(
    string Impuesto,
    string TipoFactor,
    decimal? TasaOCuota,
    bool EsRetencion);

public sealed record ComprobanteRelacionadoDto(
    Guid? Id,
    string TipoRelacion,
    Guid UuidRelacionado,
    string? Serie,
    int? Folio);

/// <summary>
/// Guarda el borrador completo de una sola vez: cabecera, conceptos y relacionados. No hay
/// autoguardado por renglón —el capturista edita la rejilla entera y guarda al final, o al
/// cambiar de pestaña— porque partir el guardado en llamadas por campo multiplicaría los
/// viajes de red sin ganar nada: nada de esto se timbra hasta que el usuario pide "Generar
/// factura".
/// </summary>
public sealed record PeticionGuardarBorrador(
    string TipoDeComprobante,
    Guid? SerieId,
    string Moneda,
    decimal? TipoCambio,
    string? FormaPago,
    string? MetodoPago,
    string Exportacion,
    string? CondicionesDePago,
    string? Observaciones,
    Guid? ClienteId,
    string? ReceptorUsoCfdi,
    string? GlobalPeriodicidad,
    string? GlobalMeses,
    int? GlobalAnio,
    IReadOnlyList<ConceptoDto> Conceptos,
    IReadOnlyList<ComprobanteRelacionadoDto> Relacionados);

/// <summary>Un CFDI relacionado resuelto por Serie+Folio, listo para agregar a la pestaña.</summary>
public sealed record CfdiRelacionadoResueltoDto(
    Guid Uuid,
    string Serie,
    int Folio);

/// <summary>Lo que devuelve <c>POST /api/documentos/{id}/timbrar</c> cuando el timbrado termina bien.</summary>
public sealed record RespuestaDeTimbradoDto(
    Guid ComprobanteId,
    Guid? Uuid,
    string? Serie,
    int? Folio,
    DateTime? FechaTimbradoUtc,
    string Estatus);

/// <summary>
/// Un renglón del listado de documentos (§1.2 del documento funcional). El folio va nulo
/// mientras el comprobante no se timbre: no se muestra antes (CLAUDE.md §5).
/// </summary>
public sealed record ComprobanteEnListaDto(
    Guid Id,
    string? Serie,
    int? Folio,
    DateTime FechaEmisionUtc,
    string ReceptorRfc,
    string ReceptorNombre,
    decimal Total,
    EstatusComprobante Estatus,
    Guid? Uuid);

/// <summary>Una página del listado de documentos, con el total de renglones que existen.</summary>
public sealed record PaginaDeComprobantes(
    IReadOnlyList<ComprobanteEnListaDto> Elementos,
    int Total);

/// <summary>
/// Lo que el usuario pide al cancelar (B8).
/// </summary>
/// <param name="Motivo">
/// <c>01</c> comprobante con errores con relación, <c>02</c> con errores sin relación,
/// <c>03</c> no se llevó a cabo la operación, <c>04</c> operación nominativa en factura
/// global. El servidor los valida; el cliente solo ofrece estos cuatro.
/// </param>
/// <param name="UuidSustituye">
/// Folio fiscal del comprobante que sustituye al cancelado. Obligatorio con el motivo
/// <c>01</c> y rechazado con cualquier otro.
/// </param>
public sealed record PeticionDeCancelacion(
    string Motivo,
    Guid? UuidSustituye);

/// <summary>Cómo quedó una solicitud de cancelación.</summary>
/// <param name="EstatusComprobante">
/// El estatus del comprobante después de la solicitud. Puede seguir en <c>en_cancelacion</c>:
/// con los motivos que exigen aceptación, el SAT solo registra la petición.
/// </param>
public sealed record ResultadoDeCancelacionDto(
    Guid ComprobanteId,
    string EstatusComprobante,
    string Motivo,
    Guid? UuidSustituye,
    string EstadoSolicitud,
    DateTime SolicitadaUtc,
    DateTime? ResueltaUtc,
    string? Mensaje);

/// <summary>Lo que contesta el SAT sobre un comprobante (§30 del documento funcional).</summary>
public sealed record EstatusSatDto(
    Guid ComprobanteId,
    Guid Uuid,
    string? EstadoCfdi,
    string? EsCancelable,
    string? EstatusCancelacion,
    string? CodigoEstatus,
    string EstatusLocal);
