using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Plataforma.Empresas;

/// <summary>
/// Lee y valida un certificado de sello digital del SAT. Todo ocurre <b>en el servidor</b>:
/// aceptar un CSD que no corresponde al contribuyente produce comprobantes que el PAC
/// rechaza, y el <c>Client</c> no puede validar nada de esto de forma confiable
/// (CLAUDE.md §3).
///
/// <para><b>Qué se comprueba y por qué</b></para>
/// <list type="bullet">
///   <item><description>Que el <c>.cer</c> sea un certificado X.509 legible.</description></item>
///   <item><description>Que sea un CSD y no la e.firma (FIEL) — ver <see cref="EsCertificadoDeSello"/>.</description></item>
///   <item><description>Que el RFC del certificado sea el de la empresa: sellar con el CSD de otro contribuyente es lo que el SAT rechaza más rápido.</description></item>
///   <item><description>Que esté vigente.</description></item>
///   <item><description>Que la contraseña abra la llave y que la llave sea <b>la de este certificado</b>: un par mal casado se descubre hasta el primer timbrado, cuando ya hay un folio apartado.</description></item>
/// </list>
/// </summary>
public static class LectorDeCsd
{
    public static Resultado<CsdLeido> Leer(byte[] cer, byte[] key, string contrasena, string rfcEmpresa)
    {
        X509Certificate2 certificado;
        try
        {
            certificado = X509CertificateLoader.LoadCertificate(cer);
        }
        catch (CryptographicException)
        {
            return ErrorNegocio.Validacion("cer-ilegible",
                "El archivo de certificado no se pudo leer. Asegúrate de subir el .cer que te dio el SAT.");
        }

        using (certificado)
        {
            if (!EsCertificadoDeSello(certificado))
                return ErrorNegocio.Validacion("cer-no-es-csd",
                    "Ese archivo parece ser tu e.firma (FIEL), no un certificado de sello digital. " +
                    "El CSD es el que se tramita aparte para facturar.");

            var rfcCertificado = RfcDelCertificado(certificado);

            if (rfcCertificado is null)
                return ErrorNegocio.Validacion("cer-sin-rfc",
                    "El certificado no trae RFC. Asegúrate de subir el .cer que te dio el SAT.");

            if (!string.Equals(rfcCertificado, rfcEmpresa, StringComparison.OrdinalIgnoreCase))
                return ErrorNegocio.Validacion("cer-de-otro-rfc",
                    $"El certificado es del RFC {rfcCertificado} y esta empresa es {rfcEmpresa}. " +
                    "No se puede sellar con el certificado de otro contribuyente.");

            // Las fechas del certificado vienen en hora local del proceso; la base guarda
            // todo en UTC (CLAUDE.md §5).
            var desde = certificado.NotBefore.ToUniversalTime();
            var hasta = certificado.NotAfter.ToUniversalTime();
            var ahora = DateTime.UtcNow;

            if (ahora < desde)
                return ErrorNegocio.Validacion("cer-aun-no-vigente",
                    $"El certificado empieza a ser válido hasta el {desde:dd/MM/yyyy}.");

            if (ahora > hasta)
                return ErrorNegocio.Validacion("cer-caducado",
                    $"El certificado caducó el {hasta:dd/MM/yyyy}. Tramita uno nuevo ante el SAT.");

            var llave = AbrirLlave(key, contrasena);

            if (llave.EsFallo)
                return llave.Error!;

            using var privada = llave.Valor;

            if (!CorrespondeAlCertificado(privada, certificado))
                return ErrorNegocio.Validacion("llave-no-corresponde",
                    "La llave privada no corresponde a este certificado. " +
                    "Revisa que el .key y el .cer sean del mismo par que te entregó el SAT.");

            return new CsdLeido(NumeroDeSerie(certificado), desde, hasta, rfcCertificado);
        }
    }

    /// <summary>
    /// Distingue el CSD de la e.firma por el uso de llave declarado en el certificado: la
    /// e.firma se emite además para cifrar (<c>KeyEncipherment</c>/<c>DataEncipherment</c>)
    /// porque sirve para autenticarse ante el SAT, mientras que el CSD se emite solo para
    /// firmar.
    /// <para>
    /// <b>Es una heurística, no una regla publicada por el SAT.</b> Es la que usan las
    /// bibliotecas de CFDI y ataja el error real —subir la FIEL creyendo que es el CSD— pero
    /// no la he podido comprobar contra un certificado real del SAT; ver el reporte de cierre
    /// de la fase 4. Si un certificado no declara uso de llave, se acepta: no vamos a
    /// bloquear un CSD legítimo por una comprobación que no es normativa.
    /// </para>
    /// </summary>
    private static bool EsCertificadoDeSello(X509Certificate2 certificado)
    {
        var usos = certificado.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();

        if (usos is null) return true;

        var cifra = usos.KeyUsages.HasFlag(X509KeyUsageFlags.KeyEncipherment)
                    || usos.KeyUsages.HasFlag(X509KeyUsageFlags.DataEncipherment);

        return !cifra;
    }

    /// <summary>
    /// El RFC vive en el sujeto del certificado, en <c>x500UniqueIdentifier</c> (OID
    /// 2.5.4.45). Para personas físicas el SAT escribe ahí «RFC / CURP», así que se toma
    /// solo lo que va antes de la diagonal.
    /// </summary>
    private static string? RfcDelCertificado(X509Certificate2 certificado)
    {
        const string oid = "2.5.4.45";

        var valor = certificado.SubjectName
            .EnumerateRelativeDistinguishedNames()
            .FirstOrDefault(rdn => rdn.GetSingleElementType().Value == oid)
            ?.GetSingleElementValue();

        // Algunos certificados lo ponen en serialNumber (2.5.4.5) en vez de en el anterior.
        valor ??= certificado.SubjectName
            .EnumerateRelativeDistinguishedNames()
            .FirstOrDefault(rdn => rdn.GetSingleElementType().Value == "2.5.4.5")
            ?.GetSingleElementValue();

        if (string.IsNullOrWhiteSpace(valor)) return null;

        return valor.Split('/', StringSplitOptions.TrimEntries)[0].Trim().ToUpperInvariant();
    }

    /// <summary>
    /// El número que viaja en el atributo <c>NoCertificado</c> del XML son veinte dígitos, y
    /// <b>no</b> es el <c>SerialNumber</c> que expone .NET: el SAT guarda esos veinte
    /// dígitos como texto ASCII, así que el hexadecimal de .NET hay que decodificarlo.
    /// «3330303031…» en hexadecimal es «30001…» en ASCII.
    /// </summary>
    private static string NumeroDeSerie(X509Certificate2 certificado)
    {
        var hexadecimal = certificado.SerialNumber;

        if (hexadecimal.Length % 2 != 0) return hexadecimal;

        var bytes = Convert.FromHexString(hexadecimal);
        var comoTexto = Encoding.ASCII.GetString(bytes);

        // Si la decodificación no da veinte dígitos, no era un serial del SAT: se devuelve
        // el hexadecimal tal cual en vez de inventar un número que iría al XML.
        return comoTexto.Length == 20 && comoTexto.All(char.IsAsciiDigit) ? comoTexto : hexadecimal;
    }

    private static Resultado<RSA> AbrirLlave(byte[] key, string contrasena)
    {
        var rsa = RSA.Create();

        try
        {
            // El .key del SAT es una llave privada PKCS#8 cifrada, en DER.
            rsa.ImportEncryptedPkcs8PrivateKey(contrasena, key, out _);
            return rsa;
        }
        catch (CryptographicException)
        {
            rsa.Dispose();

            // No se distingue «contraseña mala» de «archivo corrupto» a propósito: en los dos
            // casos el usuario tiene que revisar lo mismo, y afinar el mensaje solo serviría
            // para que alguien pruebe contraseñas y sepa cuándo va bien.
            return ErrorNegocio.Validacion("llave-no-abre",
                "No se pudo abrir la llave privada. Revisa la contraseña y que el archivo .key sea el correcto.");
        }
    }

    private static bool CorrespondeAlCertificado(RSA privada, X509Certificate2 certificado)
    {
        using var publica = certificado.GetRSAPublicKey();

        if (publica is null) return false;

        // Se comparan los parámetros públicos: si el módulo y el exponente coinciden, la
        // llave privada es la pareja de este certificado.
        var deLaLlave = privada.ExportParameters(includePrivateParameters: false);
        var delCertificado = publica.ExportParameters(includePrivateParameters: false);

        return deLaLlave.Modulus.AsSpan().SequenceEqual(delCertificado.Modulus)
               && deLaLlave.Exponent.AsSpan().SequenceEqual(delCertificado.Exponent);
    }
}

/// <summary>Lo que se pudo leer del CSD una vez que pasó todas las validaciones.</summary>
public sealed record CsdLeido(
    string NumeroSerie,
    DateTime VigenciaDesdeUtc,
    DateTime VigenciaHastaUtc,
    string Rfc);
