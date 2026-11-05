using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos.Salidas;

public sealed record RevisionDeDocumentos(bool Habilitada)
{
    public bool Admite(string estatus, string tipo) => Habilitada && estatus == "borrador" && tipo is "I" or "T" or "P";

    internal static CsdDescifradoDto CrearCertificado()
    {
        using var rsa = RSA.Create(2048);
        var peticion = new CertificateRequest("CN=CERTIFICADO SINTETICO DE REVISION SIN VALIDEZ FISCAL", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var desde = DateTimeOffset.UtcNow.AddMinutes(-5);
        var hasta = desde.AddDays(1);
        using var certificado = peticion.Create(peticion.SubjectName,
            X509SignatureGenerator.CreateForRSA(rsa, RSASignaturePadding.Pkcs1), desde, hasta,
            Encoding.ASCII.GetBytes("00000000000000000000"));
        var clave = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return new("00000000000000000000", certificado.Export(X509ContentType.Cert),
            rsa.ExportEncryptedPkcs8PrivateKey(clave, new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 10000)),
            clave, desde.UtcDateTime, hasta.UtcDateTime);
    }
}
