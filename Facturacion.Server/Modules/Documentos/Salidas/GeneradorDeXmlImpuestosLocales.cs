using System.Globalization;
using System.Xml.Linq;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>Complemento de impuestos locales: la retención de obra y el ISH.</summary>
public static class GeneradorDeXmlImpuestosLocales
{
    public static XElement Generar(IReadOnlyList<ImpuestoLocalCfdi> locales)
    {
        var espacio = XNamespace.Get(EsquemasSat.EspacioDeNombresImpuestosLocales);
        static string Cifra(decimal valor) => valor.ToString("F2", CultureInfo.InvariantCulture);

        // El XSD pide primero las retenciones y después los traslados.
        return new XElement(espacio + "ImpuestosLocales",
            new XAttribute("version", "1.0"),
            new XAttribute("TotaldeRetenciones", Cifra(locales.Where(x => x.EsRetencion).Sum(x => x.Importe))),
            new XAttribute("TotaldeTraslados", Cifra(locales.Where(x => !x.EsRetencion).Sum(x => x.Importe))),
            locales.Where(x => x.EsRetencion).Select(x => new XElement(espacio + "RetencionesLocales",
                new XAttribute("ImpLocRetenido", x.Nombre),
                new XAttribute("TasadeRetencion", Cifra(x.Tasa)),
                new XAttribute("Importe", Cifra(x.Importe)))),
            locales.Where(x => !x.EsRetencion).Select(x => new XElement(espacio + "TrasladosLocales",
                new XAttribute("ImpLocTrasladado", x.Nombre),
                new XAttribute("TasadeTraslado", Cifra(x.Tasa)),
                new XAttribute("Importe", Cifra(x.Importe)))));
    }
}
