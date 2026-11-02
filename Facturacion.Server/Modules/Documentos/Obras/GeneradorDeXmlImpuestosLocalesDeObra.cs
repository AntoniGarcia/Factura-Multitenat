using System.Globalization;
using System.Xml.Linq;
using Facturacion.Server.Modules.Documentos.Salidas;

namespace Facturacion.Server.Modules.Documentos.Obras;

public static class GeneradorDeXmlImpuestosLocalesDeObra
{
    public static XElement Generar(RetencionLocalDeObra retencion)
    {
        var importe = retencion.Importe.ToString("F2", CultureInfo.InvariantCulture);
        var espacio = XNamespace.Get(EsquemasSat.EspacioDeNombresImpuestosLocales);
        return new XElement(espacio + "ImpuestosLocales",
            new XAttribute("version", "1.0"),
            new XAttribute("TotaldeRetenciones", importe),
            new XAttribute("TotaldeTraslados", "0.00"),
            new XElement(espacio + "RetencionesLocales",
                new XAttribute("ImpLocRetenido", retencion.Nombre),
                new XAttribute("TasadeRetencion", retencion.Tasa.ToString("F2", CultureInfo.InvariantCulture)),
                new XAttribute("Importe", importe)));
    }
}
