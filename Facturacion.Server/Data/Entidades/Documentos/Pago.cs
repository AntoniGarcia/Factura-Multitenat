namespace Facturacion.Server.Data.Entidades.Documentos;

/// <summary>
/// Un pago dentro de un CFDI de tipo <c>P</c> (complemento de pagos 2.0, §27 del documento
/// funcional).
///
/// <para><b>El pago vive colgado de un comprobante, no aparte</b></para>
/// Un CFDI de pago <b>es</b> un CFDI: toma folio de una serie, se timbra, se cancela y aparece
/// en el listado igual que una factura. Por eso el padre es <see cref="Comprobante"/> con
/// <c>TipoDeComprobante = "P"</c> y no una entidad paralela: hacerlo aparte habría obligado a
/// duplicar el timbrado, la cancelación y el listado enteros.
///
/// <para><b>Lo que el SAT obliga a poner en cero</b></para>
/// En el comprobante padre, <c>SubTotal</c> y <c>Total</c> van en cero y la moneda es
/// <c>XXX</c>: el dinero del pago no se declara ahí sino aquí dentro. Ver
/// <c>GeneradorDeXmlCfdi</c>, que arma esa rama.
///
/// <para><b>Los datos bancarios no son adorno</b></para>
/// Con las formas de pago electrónicas —transferencia, cheque, tarjeta— el SAT los exige
/// (§28). Se guardan como los capturó el usuario; validar su forma es de quien construye la
/// petición, no de la entidad.
/// </summary>
public sealed class Pago : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid ComprobanteId { get; set; }

    public Comprobante Comprobante { get; set; } = null!;

    /// <summary>Momento del pago. En UTC como todo lo demás (ARQUITECTURA.md §5).</summary>
    public DateTime FechaPagoUtc { get; set; }

    /// <summary>Clave de <c>c_FormaPago</c>. Aquí <b>sí</b> es obligatoria, a diferencia de la raíz.</summary>
    public required string FormaDePagoP { get; set; }

    /// <summary>Clave de <c>c_Moneda</c> del pago, que puede diferir de la de los documentos.</summary>
    public required string MonedaP { get; set; }

    /// <summary>Obligatorio cuando <see cref="MonedaP"/> no es MXN.</summary>
    public decimal? TipoCambioP { get; set; }

    /// <summary>Importe total del pago, en <see cref="MonedaP"/>.</summary>
    public decimal Monto { get; set; }

    /// <summary>Referencia de la operación: número de transferencia, de cheque, de autorización.</summary>
    public string? NumOperacion { get; set; }

    // ── Banco ordenante (de quien paga) ──────────────────────────────────────────────

    public string? RfcEmisorCtaOrd { get; set; }

    /// <summary>Nombre del banco extranjero, cuando el ordenante no tiene RFC mexicano.</summary>
    public string? NomBancoOrdExt { get; set; }

    /// <summary>CLABE de 18, tarjeta de 16 o número de cuenta, según la forma de pago.</summary>
    public string? CtaOrdenante { get; set; }

    // ── Banco beneficiario (de quien cobra) ──────────────────────────────────────────

    public string? RfcEmisorCtaBen { get; set; }

    public string? CtaBeneficiario { get; set; }

    public List<DocumentoPagado> Documentos { get; set; } = [];
}

/// <summary>
/// Un documento que este pago abona: el <c>DoctoRelacionado</c> del complemento.
///
/// <para><b>Todo son copias, incluido el saldo</b></para>
/// <see cref="ImpSaldoAnt"/> e <see cref="ImpSaldoInsoluto"/> se congelan al timbrar el pago.
/// Recalcularlos después daría otro número —porque entretanto pudo haber más pagos— y ya no
/// coincidiría con lo que el SAT tiene sellado (ARQUITECTURA.md §5).
/// </summary>
public sealed class DocumentoPagado : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid PagoId { get; set; }

    public Pago Pago { get; set; } = null!;

    /// <summary>
    /// Solo trazabilidad hacia el comprobante pagado, igual que <c>ClienteId</c> en el
    /// comprobante. Lo que viaja al XML es <see cref="IdDocumento"/>.
    /// </summary>
    public Guid? ComprobantePagadoId { get; set; }

    /// <summary>Folio fiscal de la factura que se abona. Es la llave ante el SAT.</summary>
    public Guid IdDocumento { get; set; }

    public string? Serie { get; set; }

    public string? Folio { get; set; }

    /// <summary>Clave de <c>c_Moneda</c> de la factura pagada, que puede diferir de la del pago.</summary>
    public required string MonedaDR { get; set; }

    /// <summary>
    /// Cuántas unidades de la moneda del documento equivalen a una del pago. Va en 1 cuando
    /// las dos monedas coinciden, que es el caso normal.
    /// </summary>
    public decimal EquivalenciaDR { get; set; } = 1m;

    /// <summary>Cuántas veces se ha abonado este documento, contando esta. Empieza en 1.</summary>
    public int NumParcialidad { get; set; }

    /// <summary>Lo que se debía antes de este pago.</summary>
    public decimal ImpSaldoAnt { get; set; }

    public decimal ImpPagado { get; set; }

    /// <summary><see cref="ImpSaldoAnt"/> − <see cref="ImpPagado"/>. Nunca negativo.</summary>
    public decimal ImpSaldoInsoluto { get; set; }

    /// <summary>
    /// Clave de <c>c_ObjetoImpDR</c>: <c>01</c> no objeto de impuesto, <c>02</c> sí objeto.
    /// Con <c>02</c> el desglose de <see cref="Impuestos"/> es obligatorio.
    /// </summary>
    public required string ObjetoImpDR { get; set; }

    public List<ImpuestoDocumentoPagado> Impuestos { get; set; } = [];
}

/// <summary>
/// El desglose de impuestos de un documento pagado (<c>ImpuestosDR</c>).
///
/// <para>
/// No sale del catálogo de productos ni del motor de B1: es la parte proporcional de los
/// impuestos de la factura que corresponde a lo abonado <b>en este pago</b>. Lo calcula
/// <c>CalculoDeImpuestosDePago</c>.
/// </para>
/// </summary>
public sealed class ImpuestoDocumentoPagado : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid DocumentoPagadoId { get; set; }

    public DocumentoPagado DocumentoPagado { get; set; } = null!;

    /// <summary>Clave de <c>c_Impuesto</c>: 001 ISR, 002 IVA, 003 IEPS.</summary>
    public required string Impuesto { get; set; }

    /// <summary>Clave de <c>c_TipoFactor</c>: Tasa, Cuota o Exento.</summary>
    public required string TipoFactor { get; set; }

    /// <summary>Nulo solo con factor Exento.</summary>
    public decimal? TasaOCuota { get; set; }

    /// <summary>Parte del importe pagado sobre la que se calcula este impuesto.</summary>
    public decimal Base { get; set; }

    /// <summary>Nulo solo con factor Exento.</summary>
    public decimal? Importe { get; set; }

    public bool EsRetencion { get; set; }
}
