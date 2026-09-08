namespace Facturacion.Server.Infra.Correo;

/// <summary>
/// Plantillas de los correos de registro (código de verificación y contraseña generada).
/// Se guardan en la sección <c>Mensajes</c> de appsettings y se editan desde la UI del operador
/// como texto normal. Los marcadores son palabras en mayúsculas que el sistema sustituye por
/// el dato real al enviar, y las líneas en blanco separan párrafos.
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

    /// <summary>En el mensaje de contraseña: el correo con el que se entra.</summary>
    public const string MarcadorCorreo = "CORREO";

    /// <summary>En el mensaje de contraseña: la contraseña generada.</summary>
    public const string MarcadorClave = "CLAVE";

    public const string AsuntoVerificacionPredeterminado = "Tu código de verificación";

    public const string CuerpoVerificacionPredeterminado = """
        Hola NOMBRE:

        Para terminar de crear tu cuenta, escribe este código en la pantalla de alta:

        CODIGO

        Caduca en MINUTOS minutos. Cuando lo escribas, te mandamos la contraseña en otro mensaje.

        Si no fuiste tú quien pidió esto, ignora este mensaje: sin el código no se crea
        ninguna cuenta con tu correo.
        """;

    public const string AsuntoContrasenaPredeterminado = "Tus datos de acceso";

    public const string CuerpoContrasenaPredeterminado = """
        Hola NOMBRE:

        Tu cuenta ya está lista. Entra con estos datos:

        Correo: CORREO
        Contraseña: CLAVE

        Cámbiala en cuanto entres, desde tu perfil. Este mensaje contiene tu contraseña:
        bórralo después de guardarla en un lugar seguro.

        El siguiente paso es dar de alta tu empresa emisora para poder facturar.
        """;

    /// <summary>Asunto del correo que envía el código de verificación.</summary>
    public string AsuntoVerificacion { get; init; } = AsuntoVerificacionPredeterminado;

    public string CuerpoVerificacion { get; init; } = CuerpoVerificacionPredeterminado;

    /// <summary>Asunto del correo que envía la contraseña generada.</summary>
    public string AsuntoContrasena { get; init; } = AsuntoContrasenaPredeterminado;

    public string CuerpoContrasena { get; init; } = CuerpoContrasenaPredeterminado;
}