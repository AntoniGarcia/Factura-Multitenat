namespace Facturacion.Server.Infra.Correo;

/// <summary>
/// Plantillas de los correos de registro (código de verificación y bienvenida).
/// La sección <c>Mensajes</c> de appsettings aporta los valores iniciales; los cambios del
/// operador se persisten en SQL Server. Los marcadores son palabras en mayúsculas que el
/// sistema sustituye por el dato real al enviar, y las líneas en blanco separan párrafos.
/// </summary>
public sealed class OpcionesDeMensajes
{
    public const string Seccion = "Mensajes";

    /// <summary>En el mensaje de verificación: el nombre de quien se registra.</summary>
    public const string MarcadorNombre = "NOMBRE";

    /// <summary>En el mensaje de verificación: el código de seis dígitos.</summary>
    public const string MarcadorCodigo = "CODIGO";

    /// <summary>En el mensaje de verificación: los minutos que dura el código.</summary>
    public const string MarcadorMinutos = "MINUTOS";

    /// <summary>En el mensaje de bienvenida: el correo con el que se entra.</summary>
    public const string MarcadorCorreo = "CORREO";

    /// <summary>
    /// Compatibilidad con plantillas guardadas antes de que se eliminara el envío de
    /// contraseñas. Nunca se sustituye por una credencial real.
    /// </summary>
    public const string MarcadorClaveObsoleto = "CLAVE";

    public const string AsuntoVerificacionPredeterminado = "Tu código de verificación";

    public const string CuerpoVerificacionPredeterminado = """
        Hola NOMBRE:

        Para terminar de crear tu cuenta, escribe este código en la pantalla de alta:

        CODIGO

        Caduca en MINUTOS minutos. En la misma pantalla elegirás tu contraseña.

        Si no fuiste tú quien pidió esto, ignora este mensaje: sin el código no se crea
        ninguna cuenta con tu correo.
        """;

    public const string AsuntoContrasenaPredeterminado = "Tu cuenta está lista";

    public const string CuerpoContrasenaPredeterminado = """
        Hola NOMBRE:

        Tu cuenta ya está lista. Entra con el correo que verificaste:

        Correo: CORREO

        El siguiente paso es dar de alta tu empresa emisora para poder facturar.
        """;

    /// <summary>Asunto del correo que envía el código de verificación.</summary>
    public string AsuntoVerificacion { get; init; } = AsuntoVerificacionPredeterminado;

    public string CuerpoVerificacion { get; init; } = CuerpoVerificacionPredeterminado;

    /// <summary>Asunto del correo de bienvenida.</summary>
    public string AsuntoContrasena { get; init; } = AsuntoContrasenaPredeterminado;

    public string CuerpoContrasena { get; init; } = CuerpoContrasenaPredeterminado;
}
