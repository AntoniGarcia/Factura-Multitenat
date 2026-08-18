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
}
