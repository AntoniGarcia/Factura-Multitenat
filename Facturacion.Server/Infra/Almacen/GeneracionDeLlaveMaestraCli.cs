using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Facturacion.Server.Infra.Almacen;

/// <summary>Genera un certificado para proteger el llavero. Debe conservarse y respaldarse.</summary>
public static class GeneracionDeLlaveMaestraCli
{
    public static void Ejecutar()
    {
        using var rsa = RSA.Create(3072);

        var peticion = new CertificateRequest(
            "CN=Facturacion - llave maestra del llavero",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Vigencia larga a propósito: no se usa para autenticar a nadie, solo para cifrar el
        // llavero. Que caduque no aportaría seguridad y sí dejaría los CSD ilegibles.
        using var certificado = peticion.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(30));

        var pfx = certificado.Export(X509ContentType.Pfx);

        Console.WriteLine();
        Console.WriteLine("Copia esto en Almacen:LlaveMaestraPfx (appsettings.Development.json o variable de entorno):");
        Console.WriteLine();
        Console.WriteLine(Convert.ToBase64String(pfx));
        Console.WriteLine();
        Console.WriteLine("Guárdalo donde guardes los secretos de despliegue y respáldalo:");
        Console.WriteLine("si se pierde, los certificados de sello digital ya cargados quedan ilegibles.");
        Console.WriteLine();
    }
}
