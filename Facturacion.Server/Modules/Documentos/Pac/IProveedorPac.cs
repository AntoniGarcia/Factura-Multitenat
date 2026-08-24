namespace Facturacion.Server.Modules.Documentos.Pac;

/// <summary>Cómo terminó una llamada al PAC.</summary>
public enum ResultadoDePac
{
    /// <summary>El PAC selló el comprobante. Trae UUID, sello del SAT y XML timbrado.</summary>
    Timbrado,

    /// <summary>
    /// El PAC rechazó por un problema del comprobante. <b>Reintentar tal cual no sirve</b>:
    /// hay que corregir y volver a emitir.
    /// </summary>
    Rechazado,

    /// <summary>
    /// No se supo qué pasó: red caída, tiempo agotado, error del PAC. Puede que el
    /// comprobante esté timbrado del otro lado y no nos hayamos enterado, así que
    /// <b>nunca</b> se trata como rechazo.
    /// </summary>
    ErrorDeComunicacion,

    /// <summary>Solo para la consulta: el PAC no conoce esa clave, así que nunca lo recibió.</summary>
    NoEncontrado
}

/// <summary>Cómo terminó una solicitud de cancelación.</summary>
public enum ResultadoDeCancelacion
{
    /// <summary>El SAT la canceló. Definitivo.</summary>
    Cancelado,

    /// <summary>
    /// Aceptada, pero el receptor tiene que autorizarla. El comprobante <b>sigue siendo
    /// fiscalmente válido</b> hasta que acepte o se venza el plazo de tres días hábiles.
    /// </summary>
    EnEsperaDelReceptor,

    /// <summary>
    /// El SAT o el PAC la rechazaron: motivo que no aplica, comprobante no cancelable, fuera
    /// de plazo. Reintentar igual no sirve.
    /// </summary>
    Rechazado,

    /// <summary>
    /// No se supo qué pasó. Igual que en el timbrado, <b>nunca</b> se trata como rechazo: la
    /// cancelación pudo haber entrado, y darla por fallida dejaría vigente un comprobante que
    /// el SAT ya canceló.
    /// </summary>
    ErrorDeComunicacion
}

/// <summary>Lo que dice el SAT de un comprobante ya emitido (§30 del documento funcional).</summary>
/// <param name="EstadoCfdi">«Vigente», «Cancelado» o «No Encontrado», tal como lo nombra el SAT.</param>
/// <param name="EsCancelable">
/// «Cancelable sin aceptación», «Cancelable con aceptación» o «No cancelable». Es lo que
/// decide si cancelar será inmediato o quedará esperando al receptor.
/// </param>
/// <param name="EstatusCancelacion">Nulo mientras no haya una solicitud en curso.</param>
public sealed record EstatusSatDePac(
    bool Consultado,
    string? EstadoCfdi = null,
    string? EsCancelable = null,
    string? EstatusCancelacion = null,
    string? CodigoEstatus = null,
    string? Mensaje = null);

/// <summary>Lo que devuelve el PAC al cancelar, ya normalizado.</summary>
public sealed record RespuestaDeCancelacion(
    ResultadoDeCancelacion Resultado,
    string? CodigoRespuesta = null,
    string? Mensaje = null,
    string? Acuse = null);

/// <summary>Lo que devuelve el PAC, ya normalizado.</summary>
public sealed record RespuestaDePac(
    ResultadoDePac Resultado,
    string? XmlTimbrado = null,
    Guid? Uuid = null,
    DateTime? FechaTimbradoUtc = null,
    string? NoCertificadoSat = null,
    string? SelloSat = null,
    string? CadenaOriginalSat = null,
    string? CodigoError = null,
    string? Mensaje = null);

/// <summary>
/// La frontera con el PAC. Es una interfaz y no una clase concreta porque cada PAC del
/// mercado tiene su propia API, y la elección todavía no está tomada: todo lo caro de este
/// módulo —las transacciones cortas, la idempotencia, la conciliación— es independiente de
/// cuál sea, y esta interfaz es lo único que hay que escribir cuando se decida.
///
/// <para><b>La clave de idempotencia es el centro de todo</b></para>
/// Se genera una vez por intento de timbrado y se reutiliza en cada reintento y en la
/// consulta de conciliación. Es lo que permite volver a preguntar sin arriesgar un timbrado
/// duplicado, que es el peor resultado posible: dos folios fiscales para una sola venta,
/// dos timbres gastados y una cancelación que explicarle al SAT.
/// </summary>
public interface IProveedorPac
{
    /// <summary>Manda el XML sellado a timbrar.</summary>
    Task<RespuestaDePac> TimbrarAsync(string xml, string claveIdempotencia, CancellationToken ct);

    /// <summary>
    /// Pregunta qué pasó con una clave que se mandó y no se sabe cómo terminó. Es lo que
    /// saca del limbo a los comprobantes que quedaron en <c>timbrando</c> tras un corte.
    ///
    /// <para><b>Por qué recibe el XML si solo va a preguntar</b></para>
    /// Los PAC del mercado no exponen «dime qué pasó con esta clave»: deduplican por un
    /// identificador que se manda <b>junto con el comprobante</b>. Preguntar es, en la
    /// práctica, reenviar el mismo XML con la misma clave y leer si contestan un timbre nuevo
    /// o el que ya existía. Sin el XML no hay nada que reenviar, y el comprobante se queda en
    /// el limbo para siempre.
    /// </summary>
    /// <param name="xml">El mismo XML sellado que se envió en el intento original.</param>
    Task<RespuestaDePac> ConsultarAsync(string xml, string claveIdempotencia, CancellationToken ct);

    /// <summary>
    /// Pide al SAT la cancelación de un comprobante ya timbrado.
    ///
    /// <para>
    /// Va firmada con el CSD del emisor: el SAT no acepta que un tercero cancele por él. Por eso
    /// recibe el certificado en claro, y por eso el material nunca se registra ni se serializa
    /// (ARQUITECTURA.md §4).
    /// </para>
    /// </summary>
    /// <param name="datos">
    /// Ya validados por quien llama: el UUID sustituto es obligatorio con el motivo <c>01</c> y
    /// va nulo en los demás.
    /// </param>
    /// <param name="ct">Token de cancelación de la petición, no de la cancelación fiscal.</param>
    Task<RespuestaDeCancelacion> CancelarAsync(DatosDeCancelacion datos, CancellationToken ct);

    /// <summary>
    /// Pregunta al SAT el estado de un comprobante. Es el botón «Verificar estatus SAT» de §30
    /// y, además, lo único que saca de <c>en_cancelacion</c> a un comprobante cuya solicitud se
    /// quedó sin respuesta.
    /// </summary>
    Task<EstatusSatDePac> ConsultarEstatusAsync(DatosDeConsultaSat datos, CancellationToken ct);
}

/// <summary>Todo lo que el PAC necesita para cancelar. El CSD solo existe en memoria.</summary>
public sealed record DatosDeCancelacion(
    Guid Uuid,
    string RfcEmisor,
    string Motivo,
    Guid? UuidSustituye,
    byte[] CertificadoCer,
    byte[] LlavePrivadaKey,
    string ContrasenaLlave);

/// <summary>
/// Lo que el SAT exige para responder por un comprobante. Pide el total además del UUID
/// porque su servicio valida que quien pregunta conozca el comprobante.
/// </summary>
public sealed record DatosDeConsultaSat(
    Guid Uuid,
    string RfcEmisor,
    string RfcReceptor,
    decimal Total);
