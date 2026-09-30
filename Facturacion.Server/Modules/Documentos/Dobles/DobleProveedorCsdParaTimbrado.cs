using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos.Dobles;

/// <summary>
/// CSD inventado para <c>Pac:Modo=Simulado</c>. El XML se sella antes de ir al PAC, así que sin
/// un certificado no se puede probar el timbrado; y ninguno de prueba del SAT coincide con los
/// RFC del sembrado. Este genera, una vez por proceso, un par de llaves y un certificado
/// autofirmado con la misma forma que uno real: <c>.cer</c> en DER, <c>.key</c> en PKCS#8
/// cifrado y número de serie de veinte dígitos.
///
/// <para>
/// El sello resultante no lo acepta nadie más que <see cref="DobleProveedorPac"/>, que no lo
/// verifica. Se quita junto con él.
/// </para>
/// </summary>
[DobleDePrueba]
public sealed class DobleProveedorCsdParaTimbrado : IProveedorCsdParaTimbrado
{
    /// <summary>Todo ceros: nadie lo confunde con un número de serie emitido por el SAT.</summary>
    private const string NumeroDeSerie = "00000000000000000000";

    private readonly Lazy<CsdDescifradoDto> _material = new(Generar);

    public DobleProveedorCsdParaTimbrado(IHostEnvironment entorno)
    {
        // Segunda barrera, además de la de DocumentosModule y la verificación de contratos.
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
