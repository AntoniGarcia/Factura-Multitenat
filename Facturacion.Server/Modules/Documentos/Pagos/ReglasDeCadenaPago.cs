using System.Xml;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.Pagos;

public static class ReglasDeCadenaPago
{
    public static ErrorNegocio? Validar(string formaPago, string? tipo, string? certificado, string? cadena, string? sello)
    {
        if (new[] { tipo, certificado, cadena, sello }.All(string.IsNullOrWhiteSpace)) return null;
        if (formaPago != "03")
            return ErrorNegocio.Validacion("cadena-pago-no-aplica", "Los datos SPEI solo se incluyen en transferencias, forma de pago 03.");
        if (tipo != "01")
            return ErrorNegocio.Validacion("tipo-cadena-pago-invalido", "Selecciona el tipo de cadena 01 — SPEI.");
        if (string.IsNullOrWhiteSpace(certificado) || string.IsNullOrWhiteSpace(cadena) || string.IsNullOrWhiteSpace(sello))
            return ErrorNegocio.Validacion("cadena-pago-incompleta", "Completa el certificado, la cadena original y el sello del pago bancario.");
        if (certificado.Length > 16384 || cadena.Length > 8192 || sello.Length > 4096)
            return ErrorNegocio.Validacion("cadena-pago-demasiado-larga", "El certificado admite 16384 caracteres, la cadena 8192 y el sello 4096.");
        if (!EsBase64(certificado) || !EsBase64(sello))
            return ErrorNegocio.Validacion("cadena-pago-base64-invalida", "El certificado y el sello bancarios deben estar en formato Base64.");
        try { XmlConvert.VerifyXmlChars(cadena); }
        catch (XmlException)
        {
            return ErrorNegocio.Validacion("cadena-pago-caracteres-invalidos", "La cadena original contiene caracteres que no admite el XML.");
        }
        return null;
    }

    private static bool EsBase64(string texto)
    {
        Span<byte> bytes = stackalloc byte[12288];
        return Convert.TryFromBase64String(texto, bytes, out var cantidad) && cantidad > 0;
    }
}
