using System.Globalization;
using System.Xml.Linq;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Impuestos;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>Lo que el generador necesita y no vive dentro del comprobante.</summary>
/// <param name="FechaLocal">
/// Fecha de expedición en el huso del lugar de expedición. El SAT la pide en hora local, no
/// en UTC; la base guarda UTC, así que la conversión la hace quien orquesta.
/// </param>
/// <param name="Decimales">Decimales de la moneda, de <c>c_Moneda</c>. Dos para MXN.</param>
/// <param name="NoCertificado">Número de serie del CSD.</param>
/// <param name="CertificadoBase64">El <c>.cer</c> en base 64, sin encabezados PEM.</param>
public sealed record DatosDeEmision(
    DateTime FechaLocal,
    int Decimales,
    string NoCertificado,
    string CertificadoBase64);

/// <summary>
/// Serializa un <see cref="Comprobante"/> a XML de CFDI 4.0.
///
/// <para><b>El redondeo ocurre aquí, una sola vez</b></para>
/// El motor de impuestos trabaja a seis decimales; el XML lleva los decimales de la moneda
/// —dos para pesos—. Si cada cifra se redondeara por su cuenta al escribirla, la identidad
/// <c>Total = SubTotal − Descuento + traslados − retenciones</c> se rompería, porque
/// <c>redondear(a) + redondear(b) ≠ redondear(a+b)</c>, y el PAC valida esa identidad sobre
/// lo que dice el XML.
///
/// <para>
/// Aquí se redondea <b>solo</b> a nivel de concepto, y todos los totales son sumas de esas
/// cifras ya redondeadas. Así la identidad se cumple exactamente en la precisión en la que
/// el SAT la revisa. Ningún total se redondea por separado.
/// </para>
///
/// <para><b>Los datos salen del comprobante y de ningún otro lado</b></para>
/// No hay una sola consulta al cliente ni a la empresa: el comprobante ya trae congelados el
/// RFC, el nombre, el régimen y el domicilio (ARQUITECTURA.md §5). Es lo que hace que reimprimir
/// una factura de hace dos años dé el mismo XML que dio entonces.
/// </summary>
public sealed class GeneradorDeXmlCfdi
{
    private static readonly XNamespace Cfdi = EsquemasSat.EspacioDeNombresCfdi;
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>La tasa es un factor, no dinero: siempre seis decimales.</summary>
    private const int DecimalesDeTasa = 6;

    public XDocument Generar(Comprobante comprobante, DatosDeEmision datos)
    {
        // Un CFDI de pago no se parece a una factura: importes en cero, moneda XXX, sin nodo
        // Impuestos y con todo el dinero dentro del complemento. Se bifurca aquí y no se
        // intenta parametrizar el camino de abajo, porque casi ninguna de sus reglas aplica.
        if (comprobante.TipoDeComprobante == TiposDeComprobante.Pago)
            return GeneradorDeXmlPago.Generar(comprobante, datos);

        var d = datos.Decimales;

        // Paso único de redondeo. Todo lo que viene después suma estas cifras.
        var conceptos = comprobante.Conceptos
            .OrderBy(c => c.Orden)
            .Select(c => new
            {
                Concepto = c,
                Importe = Redondear(c.Importe, d),
                Descuento = Redondear(c.Descuento, d),
                Impuestos = c.Impuestos.Select(i => new
                {
                    Impuesto = i,
                    Base = Redondear(i.Base, d),
                    Importe = i.Importe is { } imp ? Redondear(imp, d) : (decimal?)null
                }).ToArray()
            })
            .ToArray();

        var subTotal = conceptos.Sum(c => c.Importe);
        var descuento = conceptos.Sum(c => c.Descuento);

        var planos = conceptos.SelectMany(c => c.Impuestos.Select(i => new
        {
            i.Impuesto.Impuesto,
            i.Impuesto.TipoFactor,
            i.Impuesto.TasaOCuota,
            i.Impuesto.EsRetencion,
            i.Base,
            i.Importe
        })).ToArray();

        var traslados = planos
            .Where(i => !i.EsRetencion && i.Importe is not null)
            .GroupBy(i => (i.Impuesto, i.TipoFactor, i.TasaOCuota))
            .Select(g => new
            {
                g.Key.Impuesto,
                g.Key.TipoFactor,
                g.Key.TasaOCuota,
                Base = g.Sum(x => x.Base),
                Importe = g.Sum(x => x.Importe!.Value)
            })
            .OrderBy(g => g.Impuesto).ThenBy(g => g.TasaOCuota)
            .ToArray();

        var retenciones = planos
            .Where(i => i.EsRetencion && i.Importe is not null)
            .GroupBy(i => i.Impuesto)
            .Select(g => new { Impuesto = g.Key, Importe = g.Sum(x => x.Importe!.Value) })
            .OrderBy(g => g.Impuesto)
            .ToArray();

        var totalTrasladados = traslados.Sum(t => t.Importe);
        var totalRetenidos = retenciones.Sum(r => r.Importe);
        var total = subTotal - descuento + totalTrasladados - totalRetenidos;

        var raiz = new XElement(Cfdi + "Comprobante",
            new XAttribute(XNamespace.Xmlns + "cfdi", Cfdi.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
            new XAttribute(Xsi + "schemaLocation",
                $"{Cfdi.NamespaceName} http://www.sat.gob.mx/sitio_internet/cfd/4/cfdv40.xsd"),
            new XAttribute("Version", "4.0"),
            Opcional("Serie", comprobante.Serie),
            Opcional("Folio", comprobante.Folio?.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("Fecha", datos.FechaLocal.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)),
            // Vacío a propósito: el sello se calcula sobre este documento y se pone después.
            new XAttribute("Sello", string.Empty),
            Opcional("FormaPago", comprobante.FormaPago),
            new XAttribute("NoCertificado", datos.NoCertificado),
            new XAttribute("Certificado", datos.CertificadoBase64),
            Opcional("CondicionesDePago", comprobante.CondicionesDePago),
            new XAttribute("SubTotal", Moneda(subTotal, d)),
            descuento > 0 ? new XAttribute("Descuento", Moneda(descuento, d)) : null,
            new XAttribute("Moneda", comprobante.Moneda),
            comprobante.TipoCambio is { } tc ? new XAttribute("TipoCambio", Moneda(tc, DecimalesDeTasa)) : null,
            new XAttribute("Total", Moneda(total, d)),
            new XAttribute("TipoDeComprobante", comprobante.TipoDeComprobante),
            new XAttribute("Exportacion", comprobante.Exportacion),
            Opcional("MetodoPago", comprobante.MetodoPago),
            new XAttribute("LugarExpedicion", comprobante.LugarExpedicion));

        // El orden de los hijos lo fija el XSD y no es negociable:
        // InformacionGlobal, CfdiRelacionados, Emisor, Receptor, Conceptos, Impuestos.

        if (comprobante.GlobalPeriodicidad is { } periodicidad)
        {
            raiz.Add(new XElement(Cfdi + "InformacionGlobal",
                new XAttribute("Periodicidad", periodicidad),
                new XAttribute("Meses", comprobante.GlobalMeses ?? string.Empty),
                new XAttribute("Año", comprobante.GlobalAnio?.ToString(CultureInfo.InvariantCulture) ?? string.Empty)));
        }

        // Un nodo por tipo de relación, no uno solo: en CFDI 4.0 CfdiRelacionados es
        // maxOccurs="unbounded" y el tipo de relación es atributo del padre, no del hijo.
        foreach (var grupo in comprobante.Relacionados.GroupBy(r => r.TipoRelacion).OrderBy(g => g.Key))
        {
            raiz.Add(new XElement(Cfdi + "CfdiRelacionados",
                new XAttribute("TipoRelacion", grupo.Key),
                grupo.Select(r => new XElement(Cfdi + "CfdiRelacionado",
                    new XAttribute("UUID", r.UuidRelacionado.ToString().ToUpperInvariant())))));
        }

        raiz.Add(new XElement(Cfdi + "Emisor",
            new XAttribute("Rfc", comprobante.EmisorRfc),
            new XAttribute("Nombre", comprobante.EmisorNombre),
            new XAttribute("RegimenFiscal", comprobante.EmisorRegimenFiscal)));

        raiz.Add(new XElement(Cfdi + "Receptor",
            new XAttribute("Rfc", comprobante.ReceptorRfc),
            new XAttribute("Nombre", comprobante.ReceptorNombre),
            new XAttribute("DomicilioFiscalReceptor", comprobante.ReceptorDomicilioFiscal),
            new XAttribute("RegimenFiscalReceptor", comprobante.ReceptorRegimenFiscal),
            new XAttribute("UsoCFDI", comprobante.ReceptorUsoCfdi)));

        raiz.Add(new XElement(Cfdi + "Conceptos", conceptos.Select(c =>
        {
            var nodo = new XElement(Cfdi + "Concepto",
                new XAttribute("ClaveProdServ", c.Concepto.ClaveProdServ),
                Opcional("NoIdentificacion", c.Concepto.NoIdentificacion),
                new XAttribute("Cantidad", Moneda(c.Concepto.Cantidad, DecimalesDeTasa)),
                new XAttribute("ClaveUnidad", c.Concepto.ClaveUnidad),
                Opcional("Unidad", c.Concepto.UnidadTexto),
                new XAttribute("Descripcion", c.Concepto.Descripcion),
                new XAttribute("ValorUnitario", Moneda(c.Concepto.ValorUnitario, d)),
                new XAttribute("Importe", Moneda(c.Importe, d)),
                c.Descuento > 0 ? new XAttribute("Descuento", Moneda(c.Descuento, d)) : null,
                new XAttribute("ObjetoImp", c.Concepto.ObjetoImp));

            var trasladosDelConcepto = c.Impuestos.Where(i => !i.Impuesto.EsRetencion).ToArray();
            var retencionesDelConcepto = c.Impuestos.Where(i => i.Impuesto.EsRetencion).ToArray();

            if (trasladosDelConcepto.Length > 0 || retencionesDelConcepto.Length > 0)
            {
                var impuestos = new XElement(Cfdi + "Impuestos");

                // En el concepto el orden es Traslados y luego Retenciones; en el nodo del
                // comprobante es al revés. Lo dice el XSD, no la intuición.
                if (trasladosDelConcepto.Length > 0)
                {
                    impuestos.Add(new XElement(Cfdi + "Traslados", trasladosDelConcepto.Select(i =>
                        new XElement(Cfdi + "Traslado",
                            new XAttribute("Base", Moneda(i.Base, d)),
                            new XAttribute("Impuesto", i.Impuesto.Impuesto),
                            new XAttribute("TipoFactor", i.Impuesto.TipoFactor),
                            // Exento: sin tasa y sin importe. El XSD los marca opcionales
                            // exactamente para este caso.
                            i.Impuesto.TasaOCuota is { } t ? new XAttribute("TasaOCuota", Moneda(t, DecimalesDeTasa)) : null,
                            i.Importe is { } imp ? new XAttribute("Importe", Moneda(imp, d)) : null))));
                }

                if (retencionesDelConcepto.Length > 0)
                {
                    impuestos.Add(new XElement(Cfdi + "Retenciones", retencionesDelConcepto.Select(i =>
                        new XElement(Cfdi + "Retencion",
                            new XAttribute("Base", Moneda(i.Base, d)),
                            new XAttribute("Impuesto", i.Impuesto.Impuesto),
                            new XAttribute("TipoFactor", i.Impuesto.TipoFactor),
                            new XAttribute("TasaOCuota", Moneda(i.Impuesto.TasaOCuota!.Value, DecimalesDeTasa)),
                            new XAttribute("Importe", Moneda(i.Importe!.Value, d))))));
                }

                nodo.Add(impuestos);
            }

            return nodo;
        })));

        if (traslados.Length > 0 || retenciones.Length > 0)
        {
            var impuestos = new XElement(Cfdi + "Impuestos",
                retenciones.Length > 0 ? new XAttribute("TotalImpuestosRetenidos", Moneda(totalRetenidos, d)) : null,
                traslados.Length > 0 ? new XAttribute("TotalImpuestosTrasladados", Moneda(totalTrasladados, d)) : null);

            if (retenciones.Length > 0)
            {
                impuestos.Add(new XElement(Cfdi + "Retenciones", retenciones.Select(r =>
                    new XElement(Cfdi + "Retencion",
                        new XAttribute("Impuesto", r.Impuesto),
                        new XAttribute("Importe", Moneda(r.Importe, d))))));
            }

            if (traslados.Length > 0)
            {
                impuestos.Add(new XElement(Cfdi + "Traslados", traslados.Select(t =>
                    new XElement(Cfdi + "Traslado",
                        new XAttribute("Base", Moneda(t.Base, d)),
                        new XAttribute("Impuesto", t.Impuesto),
                        new XAttribute("TipoFactor", t.TipoFactor),
                        t.TasaOCuota is { } tt ? new XAttribute("TasaOCuota", Moneda(tt, DecimalesDeTasa)) : null,
                        new XAttribute("Importe", Moneda(t.Importe, d))))));
            }

            raiz.Add(impuestos);
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), raiz);
    }

    private static XAttribute? Opcional(string nombre, string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : new XAttribute(nombre, valor);

    private static decimal Redondear(decimal valor, int decimales)
        => Math.Round(valor, decimales, MotorDeImpuestos.ModoDeRedondeo);

    private static string Moneda(decimal valor, int decimales)
        => valor.ToString("F" + decimales.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}
