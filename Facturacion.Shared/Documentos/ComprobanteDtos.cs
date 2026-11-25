using Facturacion.Shared.Comun;

namespace Facturacion.Shared.Documentos;

/// <summary>Datos del formulario de emisión. Los comprobantes timbrados se muestran en modo de solo lectura.</summary>
/// <param name="SerieId">
/// La serie elegida, que no es lo mismo que <paramref name="Serie"/>: el prefijo se copia al
/// timbrar y hasta entonces va nulo, así que sin este identificador el formulario no tiene de
/// dónde recuperar lo que el borrador traía guardado.
/// </param>
/// <param name="Variante">Una de <see cref="VariantesDeFactura"/>; nula en pagos y traslados.</param>
/// <param name="TasaIsh">Impuesto sobre hospedaje, como fracción. Nula si ningún concepto lo aplica.</param>
/// <param name="ImpuestosLocales">Los del complemento de impuestos locales; ya van dentro de <paramref name="Total"/>.</param>
public sealed record ComprobanteDto(
    Guid Id,
    string Estatus,
    string TipoDeComprobante,
    string? Variante,
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
    IReadOnlyList<ComprobanteRelacionadoDto> Relacionados,
    decimal? TasaIsh = null,
    IReadOnlyList<ImpuestoLocalDto>? ImpuestosLocales = null);

// Impuesto local (ISH o retención de obra). Tasa en porcentaje: 3.00 es el 3 %.
public sealed record ImpuestoLocalDto(string Nombre, decimal Tasa, decimal Importe, bool EsRetencion);

/// Impuestos: al guardar no se leen; el servidor los toma del producto y de Retenciones.
/// Retenciones: solo en factura básica, claves de c_Impuesto que retiene el concepto;
/// nulo deja las del producto. AplicaIsh: solo en factura básica.
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
    IReadOnlyList<ImpuestoDeConceptoDto> Impuestos,
    IReadOnlyList<string>? Retenciones = null,
    bool AplicaIsh = false,
    string? ProductoNombre = null,
    string? ProductoIdInterno = null)
{
    public DatosFiscalesDeConceptoDto? DatosFiscales { get; init; }
}

// Tasas de Configuración como fracción. Una retención en cero no muestra su casilla;
// la de IVA solo se muestra, porque cada concepto lleva la de su producto.
public sealed record TasasDeCapturaDto(decimal RetencionIva, decimal RetencionIsr, decimal RetencionIeps, decimal Ish);

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

/// Guarda el borrador completo de una vez; no hay autoguardado por renglón.
/// ReceptorNombre: solo con XAXX010101000 (ventas sueltas); con otro RFC va el del catálogo.
/// TasaIsh: fracción; solo en factura básica con algún concepto que aplica ISH.
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
    IReadOnlyList<ComprobanteRelacionadoDto> Relacionados,
    string? ReceptorNombre = null,
    decimal? TasaIsh = null);


/// <summary>Un CFDI relacionado resuelto por Serie+Folio, listo para agregar a la pestaña.</summary>
public sealed record CfdiRelacionadoResueltoDto(
    Guid Uuid,
    string Serie,
    int Folio);

// Serie con al menos un CFDI timbrado; incluye las inactivas porque sus comprobantes siguen
// vigentes. Tipos: claves de c_TipoDeComprobante que tiene timbradas.
public sealed record SerieRelacionableDto(
    string Serie,
    IReadOnlyList<string> Tipos,
    int Timbrados);

/// <summary>Lo que devuelve <c>POST /api/documentos/{id}/timbrar</c> cuando el timbrado termina bien.</summary>
public sealed record RespuestaDeTimbradoDto(
    Guid ComprobanteId,
    Guid? Uuid,
    string? Serie,
    int? Folio,
    DateTime? FechaTimbradoUtc,
    string Estatus);

/// <summary>
/// Capacidad fiscal real del servidor. <c>false</c> significa que el entorno permite trabajar
/// con borradores, pero no debe ofrecer acciones que contacten al PAC.
/// </summary>
/// <param name="Simulado">El PAC es el de desarrollo: sus timbres no tienen validez fiscal.</param>
public sealed record EstadoDeIntegracionFiscalDto(bool TimbradoDisponible, bool Simulado = false, bool RevisionDocumentos = false);

/// <summary>Resumen del documento; no muestra el folio antes del timbrado.</summary>
public sealed record ComprobanteEnListaDto(
    Guid Id,
    string? Serie,
    int? Folio,
    DateTime FechaEmisionUtc,
    string ReceptorRfc,
    string ReceptorNombre,
    decimal Total,
    EstatusComprobante Estatus,
    Guid? Uuid,
    string TipoDeComprobante);

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

/// <summary>Respuesta del SAT sobre el estado del comprobante.</summary>
public sealed record EstatusSatDto(
    Guid ComprobanteId,
    Guid Uuid,
    string? EstadoCfdi,
    string? EsCancelable,
    string? EstatusCancelacion,
    string? CodigoEstatus,
    string EstatusLocal);
