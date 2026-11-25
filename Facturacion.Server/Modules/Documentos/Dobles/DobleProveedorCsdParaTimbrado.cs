using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos.Dobles;

/// CSD inventado para Pac:Modo=Simulado: sin certificado no se puede sellar el XML.
/// Genera una vez por proceso un certificado autofirmado con la forma de uno real.
/// Solo lo acepta DobleProveedorPac y se quita junto con él.
[DobleDePrueba]
public sealed class DobleProveedorCsdParaTimbrado : IProveedorCsdParaTimbrado
{
    // Todo ceros: no se confunde con un número de serie del SAT.
    private const string NumeroDeSerie = "00000000000000000000";

    private readonly Lazy<CsdDescifradoDto> _material = new(Generar);

    public DobleProveedorCsdParaTimbrado(IHostEnvironment entorno)
    {
        // Segunda barrera, además de DocumentosModule y la verificación de contratos.
        if (!entorno.IsDevelopment())
            throw new InvalidOperationException("El CSD simulado solo existe en Development.");
    }

    public Task<CsdDescifradoDto> ObtenerAsync(CancellationToken ct) => Task.FromResult(_material.Value);

    private static CsdDescifradoDto Generar()
    {
        using var rsa = RSA.Create(2048);

        var solicitud = new CertificateRequest(
            "CN=CSD SIMULADO SIN VALIDEZ FISCAL, O=Desarrollo", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var desde = DateTimeOffset.UtcNow.AddDays(-1);
        var hasta = desde.AddYears(4);

        using var certificado = solicitud.CreateSelfSigned(desde, hasta);

        var contrasena = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var llave = rsa.ExportEncryptedPkcs8PrivateKey(
            contrasena, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 10_000));

        return new CsdDescifradoDto(
            NumeroDeSerie,
            certificado.Export(X509ContentType.Cert),
            llave,
            contrasena,
            desde.UtcDateTime,
            hasta.UtcDateTime);
    }
}
