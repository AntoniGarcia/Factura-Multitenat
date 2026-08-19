using System.Globalization;
using System.Xml.Linq;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Impuestos;
using Facturacion.Server.Modules.Documentos.Pagos;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>Las claves de <c>c_TipoDeComprobante</c> que cambian cómo se arma el XML.</summary>
public static class TiposDeComprobante
{
    public const string Ingreso = "I";
    public const string Egreso = "E";
    public const string Pago = "P";
}

/// <summary>
/// Serializa un CFDI de pago (complemento de pagos 2.0).
///
/// <para><b>Un comprobante que declara cero y no miente</b></para>
/// El SAT obliga a que la raíz vaya con <c>SubTotal="0"</c>, <c>Total="0"</c> y
/// <c>Moneda="XXX"</c> —la clave de «sin moneda»—, sin <c>FormaPago</c>, sin
/// <c>MetodoPago</c>, sin <c>TipoCambio</c>, sin <c>CondicionesDePago</c> y <b>sin</b> el nodo
/// <c>Impuestos</c>. Todo el dinero vive dentro del complemento. Poner ahí el importe del pago
/// —que es el error intuitivo— lo duplicaría en los reportes del SAT.
///
/// <para><b>El concepto es fijo y no lo elige nadie</b></para>
/// Un CFDI de pago lleva exactamente un concepto, siempre el mismo: clave <c>84111506</c>
/// («servicios de facturación»), unidad <c>ACT</c>, cantidad 1 e importe 0. No es un renglón
/// de venta, es relleno obligatorio para que el comprobante cumpla el esquema.
/// </summary>
public static class GeneradorDeXmlPago
{
    private static readonly XNamespace Cfdi = EsquemasSat.EspacioDeNombresCfdi;
    private static readonly XNamespace Pago20 = "http://www.sat.gob.mx/Pagos20";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    private const string MonedaSinValor = "XXX";
    private const string ClaveProdServDePago = "84111506";
    private const string ClaveUnidadDePago = "ACT";
    private const string NoObjetoDeImpuesto = "01";

    /// <summary>La tasa es un factor, no dinero: siempre seis decimales.</summary>
    private const int DecimalesDeTasa = 6;

    public static XDocument Generar(Comprobante comprobante, DatosDeEmision datos)
    {
        var raiz = new XElement(Cfdi + "Comprobante",
            new XAttribute(XNamespace.Xmlns + "cfdi", Cfdi.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "pago20", Pago20.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
            new XAttribute(Xsi + "schemaLocation",
                $"{Cfdi.NamespaceName} http://www.sat.gob.mx/sitio_internet/cfd/4/cfdv40.xsd " +
                $"{Pago20.NamespaceName} http://www.sat.gob.mx/sitio_internet/cfd/Pagos/Pagos20.xsd"),
            new XAttribute("Version", "4.0"),
            Opcional("Serie", comprobante.Serie),
            Opcional("Folio", comprobante.Folio?.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("Fecha", datos.FechaLocal.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)),
            // Vacío a propósito: el sello se calcula sobre este documento y se pone después.
            new XAttribute("Sello", string.Empty),
            new XAttribute("NoCertificado", datos.NoCertificado),
            new XAttribute("Certificado", datos.CertificadoBase64),
            new XAttribute("SubTotal", "0"),
            new XAttribute("Moneda", MonedaSinValor),
            new XAttribute("Total", "0"),
            new XAttribute("TipoDeComprobante", TiposDeComprobante.Pago),
            new XAttribute("Exportacion", comprobante.Exportacion),
            new XAttribute("LugarExpedicion", comprobante.LugarExpedicion));

        // Mismo orden que fija el XSD: CfdiRelacionados, Emisor, Receptor, Conceptos, Complemento.
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

        // El uso de CFDI de un pago es siempre CP01. No se toma del comprobante para que un
        // valor heredado del cliente no se cuele aquí y lo rechace el PAC.
        raiz.Add(new XElement(Cfdi + "Receptor",
            new XAttribute("Rfc", comprobante.ReceptorRfc),
            new XAttribute("Nombre", comprobante.ReceptorNombre),
            new XAttribute("DomicilioFiscalReceptor", comprobante.ReceptorDomicilioFiscal),
            new XAttribute("RegimenFiscalReceptor", comprobante.ReceptorRegimenFiscal),
            new XAttribute("UsoCFDI", UsosDeCfdi.PagosDeCfdi)));

        raiz.Add(new XElement(Cfdi + "Conceptos",
            new XElement(Cfdi + "Concepto",
                new XAttribute("ClaveProdServ", ClaveProdServDePago),
                new XAttribute("Cantidad", "1"),
                new XAttribute("ClaveUnidad", ClaveUnidadDePago),
                new XAttribute("Descripcion", "Pago"),
                new XAttribute("ValorUnitario", "0"),
                new XAttribute("Importe", "0"),
                new XAttribute("ObjetoImp", NoObjetoDeImpuesto))));

        raiz.Add(new XElement(Cfdi + "Complemento", NodoDePagos(comprobante, datos)));

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), raiz);
    }

    private static XElement NodoDePagos(Comprobante comprobante, DatosDeEmision datos)
    {
        var d = datos.Decimales;

        var pagos = comprobante.Pagos.OrderBy(p => p.FechaPagoUtc).ToArray();

        // Los Totales agregan los impuestos de TODOS los documentos de TODOS los pagos del
        // comprobante, no los de cada pago por separado.
        var impuestos = pagos
            .SelectMany(p => p.Documentos)
            .SelectMany(dr => dr.Impuestos)
            .Select(i => new ImpuestoDePagoCalculado(
                i.Impuesto, i.TipoFactor, i.TasaOCuota, i.Base, i.Importe, i.EsRetencion))
            .ToArray();

        var totales = CalculoDeImpuestosDePago.Totalizar(impuestos, pagos.Sum(p => p.Monto), d);

        var nodo = new XElement(Pago20 + "Pagos",
            new XAttribute("Version", "2.0"),
            NodoDeTotales(totales, d));

        foreach (var pago in pagos) nodo.Add(NodoDePago(pago, d));

        return nodo;
    }

    /// <summary>
    /// Cada atributo se omite cuando va en cero: declarar una base de IVA al 8 % que no existe
    /// es rechazo del PAC. <c>MontoTotalPagos</c> es el único siempre presente.
    /// </summary>
    private static XElement NodoDeTotales(TotalesDePago totales, int d)
        => new(Pago20 + "Totales",
            SiHay("TotalRetencionesIVA", totales.RetencionesIva, d),
            SiHay("TotalRetencionesISR", totales.RetencionesIsr, d),
            SiHay("TotalRetencionesIEPS", totales.RetencionesIeps, d),
            SiHay("TotalTrasladosBaseIVA16", totales.BaseIva16, d),
            SiHay("TotalTrasladosImpuestoIVA16", totales.ImpuestoIva16, d),
            SiHay("TotalTrasladosBaseIVA8", totales.BaseIva8, d),
            SiHay("TotalTrasladosImpuestoIVA8", totales.ImpuestoIva8, d),
            SiHay("TotalTrasladosBaseIVA0", totales.BaseIva0, d),
            SiHay("TotalTrasladosImpuestoIVA0", totales.ImpuestoIva0, d),
            SiHay("TotalTrasladosBaseIVAExento", totales.BaseIvaExento, d),
            new XAttribute("MontoTotalPagos", Moneda(totales.MontoTotalPagos, d)));

    private static XElement NodoDePago(Pago pago, int d)
    {
        var nodo = new XElement(Pago20 + "Pago",
            new XAttribute("FechaPago", pago.FechaPagoUtc.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)),
            new XAttribute("FormaDePagoP", pago.FormaDePagoP),
            new XAttribute("MonedaP", pago.MonedaP),
            pago.TipoCambioP is { } tc ? new XAttribute("TipoCambioP", Moneda(tc, DecimalesDeTasa)) : null,
            new XAttribute("Monto", Moneda(pago.Monto, d)),
            Opcional("NumOperacion", pago.NumOperacion),
            Opcional("RfcEmisorCtaOrd", pago.RfcEmisorCtaOrd),
            Opcional("NomBancoOrdExt", pago.NomBancoOrdExt),
            Opcional("CtaOrdenante", pago.CtaOrdenante),
            Opcional("RfcEmisorCtaBen", pago.RfcEmisorCtaBen),
            Opcional("CtaBeneficiario", pago.CtaBeneficiario));

        foreach (var documento in pago.Documentos.OrderBy(x => x.NumParcialidad).ThenBy(x => x.IdDocumento))
            nodo.Add(NodoDeDocumento(documento, d));

        return nodo;
    }

    private static XElement NodoDeDocumento(DocumentoPagado documento, int d)
    {
        var nodo = new XElement(Pago20 + "DoctoRelacionado",
            new XAttribute("IdDocumento", documento.IdDocumento.ToString().ToUpperInvariant()),
            Opcional("Serie", documento.Serie),
            Opcional("Folio", documento.Folio),
            new XAttribute("MonedaDR", documento.MonedaDR),
            new XAttribute("EquivalenciaDR", Moneda(documento.EquivalenciaDR, DecimalesDeTasa)),
            new XAttribute("NumParcialidad", documento.NumParcialidad.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("ImpSaldoAnt", Moneda(documento.ImpSaldoAnt, d)),
            new XAttribute("ImpPagado", Moneda(documento.ImpPagado, d)),
            new XAttribute("ImpSaldoInsoluto", Moneda(documento.ImpSaldoInsoluto, d)),
            new XAttribute("ObjetoImpDR", documento.ObjetoImpDR));

        if (documento.Impuestos.Count == 0) return nodo;

        var impuestos = new XElement(Pago20 + "ImpuestosDR");

        // El XSD exige retenciones antes que traslados dentro de ImpuestosDR.
        var retenciones = documento.Impuestos.Where(i => i.EsRetencion).ToArray();
        var traslados = documento.Impuestos.Where(i => !i.EsRetencion).ToArray();

        if (retenciones.Length > 0)
        {
            impuestos.Add(new XElement(Pago20 + "RetencionesDR", retenciones.Select(i =>
                new XElement(Pago20 + "RetencionDR",
                    new XAttribute("BaseDR", Moneda(i.Base, d)),
                    new XAttribute("ImpuestoDR", i.Impuesto),
                    new XAttribute("TipoFactorDR", i.TipoFactor),
                    new XAttribute("TasaOCuotaDR", Moneda(i.TasaOCuota ?? 0m, DecimalesDeTasa)),
                    new XAttribute("ImporteDR", Moneda(i.Importe ?? 0m, d))))));
        }

        if (traslados.Length > 0)
        {
            impuestos.Add(new XElement(Pago20 + "TrasladosDR", traslados.Select(i =>
                new XElement(Pago20 + "TrasladoDR",
                    new XAttribute("BaseDR", Moneda(i.Base, d)),
                    new XAttribute("ImpuestoDR", i.Impuesto),
                    new XAttribute("TipoFactorDR", i.TipoFactor),
                    // Un exento no lleva tasa ni importe; declararlos en cero es rechazo.
                    i.TipoFactor == TiposDeFactor.Exento
                        ? null
                        : new XAttribute("TasaOCuotaDR", Moneda(i.TasaOCuota ?? 0m, DecimalesDeTasa)),
                    i.TipoFactor == TiposDeFactor.Exento
                        ? null
                        : new XAttribute("ImporteDR", Moneda(i.Importe ?? 0m, d))))));
        }

        nodo.Add(impuestos);

        return nodo;
    }

    private static XAttribute? SiHay(string nombre, decimal valor, int decimales)
        => valor > 0 ? new XAttribute(nombre, Moneda(valor, decimales)) : null;

    private static XAttribute? Opcional(string nombre, string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : new XAttribute(nombre, valor);

    private static string Moneda(decimal valor, int decimales)
        => valor.ToString("F" + decimales.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}

/// <summary>Los usos de <c>c_UsoCFDI</c> que el sistema fija por su cuenta.</summary>
public static class UsosDeCfdi
{
    /// <summary>El único uso admitido en un CFDI de pago.</summary>
    public const string PagosDeCfdi = "CP01";
}
