using System.Text.RegularExpressions;

namespace Facturacion.Server.Infra.Registro;

/// <summary>
/// Reglas de enmascarado de datos sensibles. Están aquí, puras y sin dependencias, para
/// poder probarlas: son lo único que separa un RFC de un archivo de log.
/// </summary>
public static partial class EnmascaradorDeTextoSensible
{
    private const string TokenOmitido = "[token omitido]";
    private const string CertificadoOmitido = "[certificado omitido]";
    public const string ValorOmitido = "[dato sensible omitido]";

    /// <summary>
    /// Fragmentos de nombre de propiedad cuyo valor se omite completo.
    /// <para>
    /// Deliberadamente no incluye <c>clave</c> ni <c>key</c> sueltos: <c>ClaveProdServ</c>,
    /// <c>ClaveUnidad</c> e <c>Idempotency-Key</c> no son secretos, y ocultarlos dejaría
    /// los logs inservibles para diagnosticar un rechazo del PAC.
    /// </para>
    /// </summary>
    private static readonly string[] NombresSensibles =
    [
        "password", "pwd", "contrasena", "contraseña", "token", "secret", "secreto",
        "privatekey", "llaveprivada", "apikey", "authorization", "cookie", "csd", "pfx"
    ];

    /// <summary>Enmascara todo lo sensible que encuentre dentro de un texto libre.</summary>
    public static string Enmascarar(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;

        var resultado = Certificado().Replace(texto, CertificadoOmitido);
        resultado = TokenJwt().Replace(resultado, TokenOmitido);
        resultado = Rfc().Replace(resultado, coincidencia => coincidencia.Value[..3] + "**********");
        return resultado;
    }

    /// <summary>
    /// Indica si el valor de una propiedad debe omitirse completo por cómo se llama.
    /// Es la única defensa cuando el valor no tiene forma reconocible, como una contraseña.
    /// </summary>
    public static bool EsNombreSensible(string nombre)
    {
        foreach (var sensible in NombresSensibles)
            if (nombre.Contains(sensible, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    // RFC de persona moral (3 letras) o física (4 letras), seis dígitos de fecha y homoclave.
    [GeneratedRegex(@"\b[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}\b", RegexOptions.IgnoreCase)]
    private static partial Regex Rfc();

    // Un JWT siempre empieza con el encabezado {"alg": codificado en base64url.
    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*")]
    private static partial Regex TokenJwt();

    [GeneratedRegex(@"-----BEGIN [A-Z ]+-----[\s\S]*?-----END [A-Z ]+-----")]
    private static partial Regex Certificado();
}
