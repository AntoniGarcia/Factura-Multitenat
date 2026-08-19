namespace Facturacion.Shared.Documentos;

/// <summary>
/// Un CFDI de pago para el formulario de §27. Igual que <see cref="ComprobanteDto"/>, sirve
/// tanto para un borrador como para uno ya timbrado: es la misma entidad en dos momentos.
/// </summary>
/// <param name="Monto">
/// Importe total del pago. La suma de <see cref="Documentos"/> tiene que cuadrar con él; lo
/// comprueba el servidor.
/// </param>
public sealed record PagoDto(
    Guid Id,
    string Estatus,
    string? Serie,
    int? Folio,
    Guid? ClienteId,
    string? ReceptorRfc,
    string? ReceptorNombre,
    DateTime FechaPagoUtc,
    string? FormaDePagoP,
    string MonedaP,
    decimal? TipoCambioP,
    decimal Monto,
    string? NumOperacion,
    string? RfcEmisorCtaOrd,
    string? NomBancoOrdExt,
    string? CtaOrdenante,
    string? RfcEmisorCtaBen,
    string? CtaBeneficiario,
    Guid? Uuid,
    IReadOnlyList<DocumentoPagadoDto> Documentos);

/// <summary>Un renglón de la rejilla de documentos pagados (§27).</summary>
public sealed record DocumentoPagadoDto(
    Guid IdDocumento,
    string? Serie,
    string? Folio,
    string MonedaDR,
    int NumParcialidad,
    decimal ImpSaldoAnt,
    decimal ImpPagado,
    decimal ImpSaldoInsoluto,
    string ObjetoImpDR);

/// <summary>
/// Lo que devuelve el botón «Consulta» de §27 al buscar una factura por serie y folio.
/// </summary>
/// <param name="SaldoPendiente">
/// Total de la factura menos lo ya abonado en pagos timbrados. Es lo máximo que puede llevar
/// este renglón.
/// </param>
/// <param name="NumParcialidad">Qué número de parcialidad sería este pago. Empieza en 1.</param>
public sealed record DocumentoPorPagarDto(
    Guid ComprobanteId,
    Guid IdDocumento,
    string? Serie,
    string? Folio,
    string Moneda,
    decimal TotalDelDocumento,
    decimal SaldoPendiente,
    int NumParcialidad,
    string ObjetoImpDR);

/// <summary>
/// Guarda el borrador de pago completo: cabecera, banco y rejilla de documentos, de una sola
/// vez. Mismo criterio que <see cref="PeticionGuardarBorrador"/>.
/// </summary>
public sealed record PeticionGuardarPago(
    Guid ClienteId,
    DateTime FechaPagoUtc,
    string FormaDePagoP,
    string MonedaP,
    decimal? TipoCambioP,
    decimal Monto,
    string? NumOperacion,
    string? RfcEmisorCtaOrd,
    string? NomBancoOrdExt,
    string? CtaOrdenante,
    string? RfcEmisorCtaBen,
    string? CtaBeneficiario,
    IReadOnlyList<RenglonDePagoDto> Documentos);

/// <summary>
/// Un renglón que el cliente propone pagar. Solo manda el folio fiscal y el importe: el saldo
/// y el número de parcialidad los recalcula el servidor contra la base, porque entre la
/// consulta y el guardado pudo entrar otro pago.
/// </summary>
public sealed record RenglonDePagoDto(
    Guid IdDocumento,
    decimal ImpPagado);
