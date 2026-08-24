using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>XML ya sellado, con la cadena original con la que se selló.</summary>
public sealed record CfdiSellado(string Xml, string CadenaOriginal, string Sello, string NoCertificado);

/// <summary>
/// Produce el XML del CFDI: lo arma, calcula su cadena original con el XSLT del SAT, lo
/// sella con el CSD de la empresa y lo valida contra el XSD oficial.
///
/// <para><b>El orden importa y no es arbitrario</b></para>
/// <list type="number">
///   <item><description>Se arma el XML con <c>Sello</c> vacío.</description></item>
///   <item><description>Se calcula la cadena original sobre ese documento.</description></item>
///   <item><description>Se firma la cadena y el resultado se escribe en <c>Sello</c>.</description></item>
///   <item><description>Se valida contra el XSD, ya sellado, que es como viajará al PAC.</description></item>
/// </list>
/// Validar antes de sellar dejaría sin comprobar justamente el documento que se envía. Y
/// sellar antes de calcular la cadena es imposible: la cadena se extrae del XML.
///
/// <para><b>La llave privada no sale de este método</b></para>
/// Se abre, se firma y se desecha en el mismo ámbito. No se registra, no se serializa y no
/// vuelve al cliente (ARQUITECTURA.md §4).
/// </summary>
public sealed class ServicioDeXmlCfdi(
    AppDbContext baseDeDatos,
    GeneradorDeXmlCfdi generador,
    EsquemasSat esquemas,
    HusoDeEmpresa huso,
    IProveedorCsdParaTimbrado csd,
    ILogger<ServicioDeXmlCfdi> registro)
{
    public async Task<Resultado<CfdiSellado>> GenerarAsync(Comprobante comprobante, CancellationToken ct)
    {
        var esPago = comprobante.TipoDeComprobante == TiposDeComprobante.Pago;

        // Un CFDI de pago no guarda conceptos: lleva uno fijo que sintetiza el generador. Lo
        // que no puede faltarle es el pago en sí.
        if (esPago)
        {
            if (comprobante.Pagos.Count == 0)
                return ErrorNegocio.Regla("pago-sin-datos", "El comprobante de pago no tiene ningún pago.");
        }
        else if (comprobante.Conceptos.Count == 0)
        {
            return ErrorNegocio.Regla("comprobante-sin-conceptos", "El comprobante no tiene conceptos.");
        }

        // La raíz de un pago va en XXX, que no tiene decimales. Los importes que sí llevan
        // dinero viven dentro del complemento y usan la moneda del pago: tomar los decimales
        // de XXX escribiría «1160» donde debe decir «1160.00».
        var monedaDeImportes = esPago ? comprobante.Pagos[0].MonedaP : comprobante.Moneda;

        var decimales = await DecimalesDeMonedaAsync(monedaDeImportes, ct);

        if (decimales is null)
            return ErrorNegocio.Validacion(
                "moneda-desconocida",
                $"La moneda {monedaDeImportes} no está en el catálogo del SAT. ¿Se cargaron los catálogos?");

        var material = await csd.ObtenerAsync(ct);

        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(
            comprobante.FechaEmisionUtc, await huso.ObtenerAsync(ct));

        var documento = generador.Generar(comprobante, new DatosDeEmision(
            fechaLocal,
            decimales.Value,
            material.NumeroSerie,
            Convert.ToBase64String(material.CertificadoCer)));

        string cadena;

        try
        {
            cadena = CadenaOriginal(documento);
        }
        catch (FileNotFoundException ex)
        {
            // Falta un archivo del SAT: es configuración del despliegue, no culpa del usuario.
            registro.LogError(ex, "No se pudo calcular la cadena original por un artefacto del SAT ausente.");

            return ErrorNegocio.Regla(
                "esquemas-sat-incompletos",
                "Falta un archivo del SAT para calcular la cadena original. Avisa a soporte.");
        }

        var sello = Sellar(cadena, material);
        documento.Root!.SetAttributeValue("Sello", sello);

        if (Validar(documento) is { } error) return error;

        return new CfdiSellado(Serializar(documento), cadena, sello, material.NumeroSerie);
    }

    private async Task<int?> DecimalesDeMonedaAsync(string moneda, CancellationToken ct)
        => await baseDeDatos.SatMonedas
            .AsNoTracking()
            .Where(m => m.Clave == moneda)
            .Select(m => (int?)m.Decimales)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Aplica el XSLT oficial. La cadena original <b>no se construye a mano</b> a propósito:
    /// sus reglas de escapado y de omisión de campos vacíos son del SAT, y reimplementarlas
    /// produce sellos que el PAC rechaza por motivos que no se ven en el XML.
    /// </summary>
    private string CadenaOriginal(XDocument documento)
    {
        var salida = new StringWriter();

        using (var lector = documento.CreateReader())
        using (var escritor = XmlWriter.Create(salida, new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment
        }))
        {
            esquemas.CadenaOriginal.Transform(lector, escritor);
        }

        return salida.ToString();
    }

    private static string Sellar(string cadena, CsdDescifradoDto material)
    {
        using var rsa = RSA.Create();
        rsa.ImportEncryptedPkcs8PrivateKey(material.ContrasenaLlave, material.LlavePrivadaKey, out _);

        // SHA-256 con relleno PKCS#1: es lo que pide el SAT para CFDI 3.3 en adelante.
        var firma = rsa.SignData(
            Encoding.UTF8.GetBytes(cadena), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return Convert.ToBase64String(firma);
    }

    private ErrorNegocio? Validar(XDocument documento)
    {
        var problemas = new List<string>();

        var ajustes = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = esquemas.Esquema,
            DtdProcessing = DtdProcessing.Prohibit
        };

        ajustes.ValidationEventHandler += (_, e) => problemas.Add(e.Message);

        using (var lector = XmlReader.Create(new StringReader(Serializar(documento)), ajustes))
        {
            while (lector.Read()) { }
        }

        if (problemas.Count == 0) return null;

        // Al log el detalle completo; al usuario, que el comprobante no cumple el estándar.
        // Un mensaje de XSD no le dice nada a un contador y sí revela la estructura interna.
        registro.LogError(
            "El CFDI generado no cumple el XSD del SAT: {Problemas}", string.Join(" | ", problemas));

        return ErrorNegocio.Regla(
            "cfdi-no-cumple-el-estandar",
            "El comprobante generado no cumple el estándar del SAT. El detalle quedó en la bitácora técnica.");
    }

    private static string Serializar(XDocument documento)
    {
        // UTF-8 sin BOM: el SAT lo exige y un BOM invalida el sello.
        var salida = new StringWriter();

        using (var escritor = XmlWriter.Create(salida, new XmlWriterSettings
        {
            Indent = false,
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false
        }))
        {
            documento.Save(escritor);
        }

        return salida.ToString();
    }
}
