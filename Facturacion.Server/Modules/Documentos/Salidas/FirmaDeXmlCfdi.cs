using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos.Salidas;

internal static class FirmaDeXmlCfdi
{
    internal static string CadenaOriginal(XDocument documento, EsquemasSat esquemas)
    {
        using var salida = new StringWriter();
        using var lector = documento.CreateReader();
        using (var escritor = XmlWriter.Create(salida, new XmlWriterSettings
        { OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment }))
            esquemas.CadenaOriginal.Transform(lector, escritor);
        return salida.ToString();
    }

    internal static string Sellar(string cadena, CsdDescifradoDto material)
    {
        using var rsa = RSA.Create();
        rsa.ImportEncryptedPkcs8PrivateKey(material.ContrasenaLlave, material.LlavePrivadaKey, out _);
        return Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(cadena), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    internal static string Serializar(XDocument documento)
    {
        using var salida = new MemoryStream();
        using (var escritor = XmlWriter.Create(salida, new XmlWriterSettings
        { Indent = false, Encoding = new UTF8Encoding(false), OmitXmlDeclaration = false }))
            documento.Save(escritor);
        return Encoding.UTF8.GetString(salida.ToArray());
    }
}
