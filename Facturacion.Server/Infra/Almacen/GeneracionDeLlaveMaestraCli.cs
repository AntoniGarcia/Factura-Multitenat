using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Facturacion.Server.Infra.Almacen;

/// <summary>
/// <c>dotnet run -- --generar-llave-maestra</c>. Imprime un certificado nuevo en base 64
/// para <c>Almacen:LlaveMaestraPfx</c>, que es lo que protege el llavero de Data Protection
/// y, con él, los CSD guardados (CLAUDE.md §4).
/// <para>
/// Existe como comando y no como generación automática al arrancar a propósito: una clave
/// maestra que la aplicación se inventa sola y guarda junto a lo que protege no protege de
/// nada. Al obligar a copiarla a la configuración, queda claro que es un secreto de
/// despliegue y que hay que respaldarlo.
/// </para>
/// <para>
/// <b>Perderla es perder los certificados.</b> Sin ella el llavero no se puede leer, y sin
/// el llavero los <c>.cer</c> y <c>.key</c> guardados quedan ilegibles y hay que volver a
/// cargarlos.
/// </para>
/// </summary>
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
