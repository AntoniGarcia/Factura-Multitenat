using System.Xml.Linq;
using System.Xml.Schema;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos.Timbrado;

public sealed class XmlDeFacturaProveedor(GeneradorDeXmlCfdi generador, EsquemasSat esquemas)
{
    public Resultado<XmlFacturaProveedorDto> Generar(IntentoFacturaCompraDto intento, CsdDescifradoDto material)
    {
        var f = intento.Datos;
        var zona = TimeZoneInfo.FindSystemTimeZoneById(f.Emisor.ZonaHoraria);
        var c = new Comprobante
        {
            Id = intento.Id, Estatus = "timbrando", TipoDeComprobante = "I", Moneda = "MXN", MetodoPago = "PUE",
            FormaPago = f.FormaPago, Exportacion = "01", Serie = f.Emisor.Serie, Folio = intento.Folio,
            FechaEmisionUtc = intento.FechaEmisionUtc, LugarExpedicion = f.Emisor.CodigoPostal,
            EmisorRfc = f.Emisor.Rfc, EmisorNombre = f.Emisor.Nombre, EmisorRegimenFiscal = f.Emisor.Regimen,
            ReceptorRfc = f.Receptor.Rfc, ReceptorNombre = f.Receptor.Nombre, ReceptorRegimenFiscal = f.Receptor.Regimen,
            ReceptorDomicilioFiscal = f.Receptor.CodigoPostal, ReceptorUsoCfdi = f.Receptor.UsoCfdi,
            SubTotal = Math.Round(f.Subtotal, 2, MidpointRounding.AwayFromZero),
            TotalImpuestosTrasladados = Math.Round(f.Iva, 2, MidpointRounding.AwayFromZero),
            Total = Math.Round(f.Total, 2, MidpointRounding.AwayFromZero),
            Conceptos = [new Concepto
            {
                Orden = 1, ClaveProdServ = f.ClaveProdServ, ClaveUnidad = f.ClaveUnidad,
                Descripcion = f.Descripcion, Cantidad = 1, ValorUnitario = f.Subtotal,
                Importe = f.Subtotal, ObjetoImp = "02",
                Impuestos = [new ImpuestoConcepto
                { Impuesto = "002", TipoFactor = "Tasa", TasaOCuota = f.TasaIva, Base = f.Subtotal, Importe = f.Iva }]
            }]
        };
        var doc = generador.Generar(c, new(TimeZoneInfo.ConvertTimeFromUtc(intento.FechaEmisionUtc, zona), 2,
            material.NumeroSerie, Convert.ToBase64String(material.CertificadoCer)) { ZonaHoraria = zona });
        var cadena = FirmaDeXmlCfdi.CadenaOriginal(doc, esquemas);
        var sello = FirmaDeXmlCfdi.Sellar(cadena, material);
        doc.Root!.SetAttributeValue("Sello", sello);
        var valido = true;
        doc.Validate(esquemas.Esquema, (_, _) => valido = false);
        return valido ? new XmlFacturaProveedorDto(FirmaDeXmlCfdi.Serializar(doc), cadena, sello, material.NumeroSerie)
            : ErrorNegocio.Regla("xml-proveedor-invalido", "La factura no cumple el esquema del SAT. Revisa sus datos.");
    }

    public static bool CoincideTimbre(XmlFacturaProveedorDto enviado, Pac.RespuestaDePac respuesta)
    {
        if (respuesta.Uuid is null || respuesta.FechaTimbradoUtc is null || string.IsNullOrWhiteSpace(respuesta.XmlTimbrado)
            || respuesta.XmlTimbrado.Length > 120_000) return false;
        try
        {
            var original = XDocument.Parse(enviado.Xml);
            var recibido = XDocument.Parse(respuesta.XmlTimbrado);
            XNamespace cfdi = EsquemasSat.EspacioDeNombresCfdi;
            XNamespace tfd = "http://www.sat.gob.mx/TimbreFiscalDigital";
            var raiz = recibido.Root;
            var timbres = raiz?.Element(cfdi + "Complemento")?.Elements(tfd + "TimbreFiscalDigital").ToList();
            if (raiz?.Name != cfdi + "Comprobante" || timbres?.Count != 1) return false;
            var timbre = timbres[0];
            if (!Guid.TryParse((string?)timbre.Attribute("UUID"), out var uuid) || uuid != respuesta.Uuid ||
                (string?)timbre.Attribute("SelloCFD") != enviado.Sello ||
                (string?)timbre.Attribute("SelloSAT") != respuesta.SelloSat ||
                (string?)timbre.Attribute("NoCertificadoSAT") != respuesta.NoCertificadoSat) return false;
            timbre.Remove();
            var complemento = raiz.Element(cfdi + "Complemento");
            if (complemento is not null && !complemento.HasElements && !complemento.HasAttributes) complemento.Remove();
            // El PAC puede agregar declaraciones de espacios de nombres; no puede cambiar el CFDI firmado.
            foreach (var d in new[] { original, recibido })
                foreach (var a in d.Descendants().Attributes().Where(x => x.IsNamespaceDeclaration ||
                    x.Name == XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "schemaLocation").ToList()) a.Remove();
            return XNode.DeepEquals(original.Root, recibido.Root);
        }
        catch (System.Xml.XmlException) { return false; }
    }
}
