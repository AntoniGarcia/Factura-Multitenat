using System.Globalization;
using System.Xml.Linq;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Salidas;

namespace Facturacion.Server.Modules.Documentos.Obras;

public static class GeneradorDeXmlImpuestosLocalesDeObra
{
    public static XElement Generar(Comprobante comprobante)
    {
        var importe = Math.Round(comprobante.SubTotal * 0.005m, 2, MidpointRounding.ToEven);
        var espacio = XNamespace.Get(EsquemasSat.EspacioDeNombresImpuestosLocales);
        return new XElement(espacio + "ImpuestosLocales",
            new XAttribute("version", "1.0"),
            new XAttribute("TotaldeRetenciones", importe.ToString("F2", CultureInfo.InvariantCulture)),
            new XAttribute("TotaldeTraslados", "0.00"),
            new XElement(espacio + "RetencionesLocales",
                new XAttribute("ImpLocRetenido", "5 al millar"),
                new XAttribute("TasadeRetencion", "0.50"),
                new XAttribute("Importe", importe.ToString("F2", CultureInfo.InvariantCulture))));
    }
}
