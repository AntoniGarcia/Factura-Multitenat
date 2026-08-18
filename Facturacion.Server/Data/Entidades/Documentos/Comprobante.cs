namespace Facturacion.Server.Data.Entidades.Documentos;

/// <summary>
/// Un CFDI: borrador, timbrado, cancelado o fallido. Es la entidad central de la mitad B.
///
/// <para><b>Por qué casi todo aquí es una copia y no una llave foránea</b></para>
/// Los datos fiscales del emisor y del receptor viven <b>dentro</b> del comprobante, no
/// apuntados por relación (CLAUDE.md §5). Al timbrar se congelan: si el cliente cambia de
/// domicilio en 2028, la factura de 2026 tiene que seguir diciendo lo que decía cuando el
/// SAT la selló. Una llave foránea haría justo lo contrario — reflejar el dato de hoy — y
/// convertiría cada reporte histórico en una mentira distinta cada vez que se consulta.
///
/// <para>
/// <see cref="ClienteId"/> y el <c>ProductoId</c> de cada concepto se conservan solo para
/// trazabilidad —«¿qué facturas salieron de este cliente?»— y por eso son identificadores
/// sueltos, sin relación ni navegación. Nada de la emisión los lee para obtener datos.
/// </para>
///
/// <para><b>Folio</b></para>
/// Nulo hasta que se reserva, y no se enseña antes de timbrar (CLAUDE.md §5). Si el
/// timbrado falla después de tomarlo, el comprobante queda en <c>error</c> con su folio
/// apartado: no se recicla.
/// </summary>
public sealed class Comprobante : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    /// <summary>Uno de <c>EstatusComprobante</c>, en su forma de cadena. Solo esos seis.</summary>
    public required string Estatus { get; set; }

    // ── Serie y folio ────────────────────────────────────────────────────────────────

    /// <summary>Serie de la que salió el folio. Suelto: la serie puede renombrarse después.</summary>
    public Guid? SerieId { get; set; }

    /// <summary>Copia del texto de la serie en el momento de reservar.</summary>
    public string? Serie { get; set; }

    public int? Folio { get; set; }

    // ── Identidad ante el SAT ────────────────────────────────────────────────────────

    /// <summary>Folio fiscal. Solo existe después de timbrar.</summary>
    public Guid? Uuid { get; set; }

    // ── Cabecera del CFDI ────────────────────────────────────────────────────────────

    /// <summary>Clave de <c>c_TipoDeComprobante</c>: I, E, T, N o P.</summary>
    public required string TipoDeComprobante { get; set; }

    /// <summary>Fecha de emisión. En UTC en la base; se muestra en el huso de la empresa.</summary>
    public DateTime FechaEmisionUtc { get; set; }

    /// <summary>Código postal del lugar de expedición, copiado de la empresa al emitir.</summary>
    public required string LugarExpedicion { get; set; }

    /// <summary>Clave de <c>c_Moneda</c>.</summary>
    public required string Moneda { get; set; }

    /// <summary>Obligatorio cuando la moneda no es MXN; nulo cuando lo es.</summary>
    public decimal? TipoCambio { get; set; }

    /// <summary>Clave de <c>c_FormaPago</c>. Opcional en algunos tipos de comprobante.</summary>
    public string? FormaPago { get; set; }

    /// <summary>Clave de <c>c_MetodoPago</c>: PUE o PPD.</summary>
    public string? MetodoPago { get; set; }

    /// <summary>Clave de <c>c_Exportacion</c>. Obligatoria en CFDI 4.0.</summary>
    public required string Exportacion { get; set; }

    /// <summary>Condiciones de pago en texto libre, tal como las capturó el usuario.</summary>
    public string? CondicionesDePago { get; set; }

    // ── Emisor, congelado ────────────────────────────────────────────────────────────

    public required string EmisorRfc { get; set; }

    public required string EmisorNombre { get; set; }

    public required string EmisorRegimenFiscal { get; set; }

    // ── Receptor, congelado ──────────────────────────────────────────────────────────

    /// <summary>Solo trazabilidad. Ningún dato del comprobante se lee desde aquí.</summary>
    public Guid? ClienteId { get; set; }

    public required string ReceptorRfc { get; set; }

    /// <summary>Ya normalizado: mayúsculas, sin acentos y sin régimen de capital (CLAUDE.md §7).</summary>
    public required string ReceptorNombre { get; set; }

    public required string ReceptorRegimenFiscal { get; set; }

    /// <summary>Código postal de la constancia de situación fiscal, no el de entrega.</summary>
    public required string ReceptorDomicilioFiscal { get; set; }

    /// <summary>Clave de <c>c_UsoCFDI</c>.</summary>
    public required string ReceptorUsoCfdi { get; set; }

    // ── Información global (público en general) ──────────────────────────────────────
    //
    // Nodo opcional del CFDI, no una entidad aparte: es a lo sumo un renglón por
    // comprobante y solo aplica cuando el receptor es el RFC genérico XAXX010101000.

    /// <summary>Clave de <c>c_Periodicidad</c>.</summary>
    public string? GlobalPeriodicidad { get; set; }

    /// <summary>Clave de <c>c_Meses</c>.</summary>
    public string? GlobalMeses { get; set; }

    public int? GlobalAnio { get; set; }

    // ── Totales ──────────────────────────────────────────────────────────────────────
    //
    // Los calcula el motor de impuestos sumando por concepto, nunca aplicando la tasa al
    // total: esa diferencia de redondeo es la causa número uno de rechazo del PAC.

    public decimal SubTotal { get; set; }

    public decimal Descuento { get; set; }

    public decimal TotalImpuestosTrasladados { get; set; }

    public decimal TotalImpuestosRetenidos { get; set; }

    public decimal Total { get; set; }

    // ── Resultado del timbrado ───────────────────────────────────────────────────────

    public DateTime? FechaTimbradoUtc { get; set; }

    /// <summary>Número de serie del CSD con el que se selló. Copia, no relación.</summary>
    public string? NoCertificadoEmisor { get; set; }

    public string? NoCertificadoSat { get; set; }

    public string? SelloCfd { get; set; }

    public string? SelloSat { get; set; }

    /// <summary>Cadena original del complemento de certificación, la que devuelve el PAC.</summary>
    public string? CadenaOriginalSat { get; set; }

    /// <summary>
    /// Ruta del XML timbrado dentro del almacén cifrado. Nunca en <c>wwwroot</c>: se sirve
    /// por endpoint autorizado que valida la empresa (CLAUDE.md §4).
    /// </summary>
    public string? RutaXml { get; set; }

    // ── Auditoría ────────────────────────────────────────────────────────────────────

    public DateTime CreadoUtc { get; set; }

    public DateTime? ModificadoUtc { get; set; }

    public Guid CreadoPorUsuarioId { get; set; }

    public List<Concepto> Conceptos { get; set; } = [];

    public List<ComprobanteRelacionado> Relacionados { get; set; } = [];

    public List<IntentoTimbrado> Intentos { get; set; } = [];
}
