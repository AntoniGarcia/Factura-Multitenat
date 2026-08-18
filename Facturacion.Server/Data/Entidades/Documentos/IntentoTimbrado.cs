namespace Facturacion.Server.Data.Entidades.Documentos;

/// <summary>
/// Un intento de timbrar contra el PAC, con lo que se pidió y lo que contestó.
///
/// <para><b>Para qué sirve tener esto y no solo el resultado final</b></para>
/// El timbrado son tres pasos y la llamada al PAC ocurre <b>fuera</b> de toda transacción,
/// porque puede tardar treinta segundos y una transacción abierta ese tiempo tumba el
/// sistema. La consecuencia es que un corte de red deja al comprobante en <c>timbrando</c>
/// sin que nadie sepa si el PAC alcanzó a sellarlo. Este renglón es lo que permite que la
/// conciliación posterior sepa qué se envió, cuándo, y con qué clave de idempotencia
/// preguntarle al PAC qué pasó — en vez de reintentar a ciegas y arriesgar un duplicado.
/// </para>
///
/// <para>
/// Nunca se borra ni se sobrescribe: cada intento agrega un renglón. Un comprobante con
/// cinco intentos y un timbre cuenta una historia que un solo campo de estado no puede.
/// </para>
/// </summary>
public sealed class IntentoTimbrado : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid ComprobanteId { get; set; }

    public Comprobante Comprobante { get; set; } = null!;

    /// <summary>Número de intento para este comprobante, empezando en 1.</summary>
    public int Numero { get; set; }

    public DateTime IniciadoUtc { get; set; }

    /// <summary>Nulo mientras el intento sigue en vuelo o murió sin respuesta.</summary>
    public DateTime? TerminadoUtc { get; set; }

    /// <summary>Uno de <see cref="ResultadosDeIntento"/>.</summary>
    public required string Resultado { get; set; }

    /// <summary>
    /// La misma clave que se mandó al PAC. Es lo que permite repreguntar por este intento
    /// concreto sin provocar un timbrado duplicado.
    /// </summary>
    public string? ClaveIdempotencia { get; set; }

    /// <summary>
    /// El XML sellado tal como salió hacia el PAC.
    ///
    /// <para><b>Por qué se guarda y no se vuelve a generar</b></para>
    /// La conciliación pregunta reenviando este mismo documento con la misma clave. Regenerarlo
    /// desde el comprobante daría un XML <b>distinto</b> si entre el envío y la conciliación
    /// cambió el CSD activo de la empresa —una renovación de certificado basta—, y lo que hay
    /// que reenviar es lo que se mandó, no algo parecido.
    /// </summary>
    public string? XmlEnviado { get; set; }

    /// <summary>Código de error del PAC, tal cual lo devolvió.</summary>
    public string? CodigoError { get; set; }

    /// <summary>Mensaje del PAC. Se guarda para soporte; no se le enseña crudo al usuario.</summary>
    public string? MensajeError { get; set; }

    public int? DuracionMs { get; set; }
}

/// <summary>Resultados de un intento. Cadenas fijas para poder filtrarlas sin adivinar.</summary>
public static class ResultadosDeIntento
{
    /// <summary>Enviado y todavía sin respuesta. Es el estado que busca la conciliación.</summary>
    public const string EnVuelo = "en_vuelo";

    public const string Timbrado = "timbrado";

    /// <summary>El PAC rechazó por un problema del comprobante. Reintentar igual no sirve.</summary>
    public const string Rechazado = "rechazado";

    /// <summary>Fallo de red o del PAC. Puede reintentarse con la misma clave de idempotencia.</summary>
    public const string ErrorDeComunicacion = "error_de_comunicacion";
}
