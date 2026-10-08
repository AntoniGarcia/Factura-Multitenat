using System.Globalization;
using System.Xml.Linq;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Impuestos;
using Facturacion.Server.Modules.Documentos.Pagos;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>Las claves de <c>c_TipoDeComprobante</c> que cambian cómo se arma el XML.</summary>
public static class TiposDeComprobante
{
    public const string Ingreso = "I";
    public const string Egreso = "E";
    public const string Traslado = "T";
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
    private static readonly XNamespace Pago20 = EsquemasSat.EspacioDeNombresPagos20;
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    private const string MonedaSinValor = "XXX";
    private const string ClaveProdServDePago = "84111506";
    private const string ClaveUnidadDePago = "ACT";
    private const string NoObjetoDeImpuesto = "01";

    /// <summary>La tasa es un factor, no dinero: siempre seis decimales.</summary>
    private const int DecimalesDeTasa = 6;

    public static ErrorNegocio? Validar(Comprobante comprobante, int? decimalesMoneda = null)
    {
        if (comprobante.Pagos.Count == 0 || comprobante.Pagos.Any(p => p.Documentos.Count == 0))
            return ErrorNegocio.Validacion("pago-sin-datos", "Guarda el pago con al menos una factura relacionada.");
        if (comprobante.Pagos.Select(p => p.MonedaP).Distinct().Count() != 1)
            return ErrorNegocio.Validacion("monedas-de-pago-distintas", "Esta captura admite una sola moneda de pago por comprobante.");

        foreach (var pago in comprobante.Pagos)
        {
            if (pago.MonedaP == "XXX" || pago.Monto <= 0 || pago.FechaPagoUtc == default || pago.FormaDePagoP == "99")
                return ErrorNegocio.Validacion("pago-datos-invalidos", "Revisa la moneda, el importe y la fecha del pago.");
            if (pago.MonedaP != "MXN" && pago.TipoCambioP is not > 0m)
                return ErrorNegocio.Validacion("tipo-cambio-pago-invalido", "El pago en moneda extranjera necesita un tipo de cambio positivo a MXN.");
            if ((pago.MonedaP == "MXN" && pago.TipoCambioP is { } cambioMxn && cambioMxn != 1m) ||
                (pago.TipoCambioP is { } cambio && Math.Round(cambio, 6) != cambio))
                return ErrorNegocio.Validacion("tipo-cambio-pago-invalido", "Revisa el tipo de cambio: en MXN es 1 y admite hasta seis decimales.");
            if (decimalesMoneda is { } precision &&
                (Math.Round(pago.Monto, precision) != pago.Monto || pago.Documentos.Any(d =>
                    Math.Round(d.ImpPagado, precision) != d.ImpPagado ||
                    Math.Round(d.ImpSaldoAnt, precision) != d.ImpSaldoAnt ||
                    Math.Round(d.ImpSaldoInsoluto, precision) != d.ImpSaldoInsoluto)))
                return ErrorNegocio.Validacion("precision-pago-invalida", "Los saldos guardados no respetan los decimales de la moneda. Guarda nuevamente el borrador.");
            if (pago.Documentos.Select(d => d.IdDocumento).Distinct().Count() != pago.Documentos.Count)
                return ErrorNegocio.Validacion("documento-repetido", "El pago contiene facturas duplicadas. Guarda nuevamente el borrador.");

            foreach (var documento in pago.Documentos)
            {
                if (documento.MonedaDR != pago.MonedaP || documento.EquivalenciaDR != 1m)
                    return ErrorNegocio.Validacion("monedas-distintas-no-admitidas", "La captura actual requiere que el pago y las facturas estén en la misma moneda, con equivalencia 1.");
                if (documento.IdDocumento == Guid.Empty || documento.NumParcialidad < 1 ||
                    documento.ImpPagado <= 0 || documento.ImpSaldoAnt < documento.ImpPagado ||
                    documento.ImpSaldoInsoluto != documento.ImpSaldoAnt - documento.ImpPagado)
                    return ErrorNegocio.Validacion("saldos-pago-invalidos", "Los saldos o la parcialidad del pago son inconsistentes. Guarda nuevamente el borrador.");
                if ((documento.ObjetoImpDR == "02" && documento.Impuestos.Count == 0) ||
                    (documento.ObjetoImpDR != "02" && documento.Impuestos.Count > 0))
                    return ErrorNegocio.Validacion("impuestos-pago-inconsistentes", "El objeto de impuesto del documento no coincide con su desglose. Guarda nuevamente el borrador.");
                if (documento.Impuestos.Any(i => i.Base < 0 || i.Importe < 0 ||
                    (i.TipoFactor == TiposDeFactor.Exento && (i.EsRetencion || i.TasaOCuota is not null || i.Importe is not null)) ||
                    (i.TipoFactor != TiposDeFactor.Exento && (i.Importe is null || i.TasaOCuota is null))))
                    return ErrorNegocio.Validacion("impuestos-pago-invalidos", "El desglose del pago tiene impuestos incompletos o negativos.");
                if (documento.Impuestos.Any(i => i.TipoFactor == TiposDeFactor.Tasa &&
                    RedondearImpuesto(i.Base * i.TasaOCuota!.Value) != i.Importe))
                    return ErrorNegocio.Validacion("impuestos-pago-desactualizados", "El desglose guardado no coincide con el cálculo a seis decimales. Guarda nuevamente el borrador.");
            }
            if (pago.Monto != pago.Documentos.Sum(d => d.ImpPagado))
                return ErrorNegocio.Validacion("pago-no-cuadra", "El monto del pago no coincide con lo abonado a las facturas. Guarda nuevamente el borrador.");
        }
        return null;
    }

    public static XDocument Generar(Comprobante comprobante, DatosDeEmision datos)
    {
        if (Validar(comprobante, datos.Decimales) is { } problema)
            throw new InvalidOperationException(problema.Mensaje);
        if (datos.ZonaHoraria is null)
            throw new InvalidOperationException("Falta el huso de la empresa para convertir la fecha del pago.");
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

        var pagos = comprobante.Pagos.OrderBy(p => p.FechaPagoUtc)
            .Select(p => (Pago: p, Cambio: CambioAMxn(p), Impuestos: AgruparImpuestos(p)))
            .ToArray();

        // Totales se obtiene de los mismos grupos que se serializan en ImpuestosP, pero en MXN.
        var impuestos = pagos.SelectMany(p => p.Impuestos.Select(i => i with
            {
                Base = i.Base * p.Cambio,
                Importe = i.Importe * p.Cambio
            }))
            .ToArray();

        var totales = CalculoDeImpuestosDePago.Totalizar(impuestos,
            pagos.Sum(p => p.Pago.Monto * p.Cambio), 2);

        var nodo = new XElement(Pago20 + "Pagos",
            new XAttribute("Version", "2.0"),
            NodoDeTotales(totales, impuestos));

        foreach (var pago in pagos)
            nodo.Add(NodoDePago(pago.Pago, d, datos.ZonaHoraria!, pago.Impuestos));

        return nodo;
    }

    /// <summary>
    /// Los atributos dependen de los grupos existentes, no de que su importe sea positivo:
    /// un traslado a tasa 0 sí requiere declarar su impuesto en cero.
    /// </summary>
    private static XElement NodoDeTotales(TotalesDePago totales, IReadOnlyList<ImpuestoDePagoCalculado> impuestos)
    {
        bool Retencion(string clave) => impuestos.Any(i => i.EsRetencion && i.Impuesto == clave);
        bool Iva(decimal tasa) => impuestos.Any(i => !i.EsRetencion && i.Impuesto == "002" &&
            i.TipoFactor == TiposDeFactor.Tasa && i.TasaOCuota == tasa);
        XAttribute? Dato(string nombre, decimal valor, bool existe)
            => existe ? new XAttribute(nombre, Moneda(valor, 2)) : null;

        return new XElement(Pago20 + "Totales",
            Dato("TotalRetencionesIVA", totales.RetencionesIva, Retencion("002")),
            Dato("TotalRetencionesISR", totales.RetencionesIsr, Retencion("001")),
            Dato("TotalRetencionesIEPS", totales.RetencionesIeps, Retencion("003")),
            Dato("TotalTrasladosBaseIVA16", totales.BaseIva16, Iva(0.16m)),
            Dato("TotalTrasladosImpuestoIVA16", totales.ImpuestoIva16, Iva(0.16m)),
            Dato("TotalTrasladosBaseIVA8", totales.BaseIva8, Iva(0.08m)),
            Dato("TotalTrasladosImpuestoIVA8", totales.ImpuestoIva8, Iva(0.08m)),
            Dato("TotalTrasladosBaseIVA0", totales.BaseIva0, Iva(0m)),
            Dato("TotalTrasladosImpuestoIVA0", totales.ImpuestoIva0, Iva(0m)),
            Dato("TotalTrasladosBaseIVAExento", totales.BaseIvaExento,
                impuestos.Any(i => !i.EsRetencion && i.Impuesto == "002" && i.TipoFactor == TiposDeFactor.Exento)),
            new XAttribute("MontoTotalPagos", Moneda(totales.MontoTotalPagos, 2)));
    }

    internal static decimal CambioAMxn(Pago pago)
        => pago.MonedaP == "MXN" ? 1m : pago.TipoCambioP is > 0m
            ? pago.TipoCambioP.Value
            : throw new InvalidOperationException("Falta un tipo de cambio válido del pago a MXN.");

    internal static IReadOnlyList<ImpuestoDePagoCalculado> AgruparImpuestos(Pago pago)
    {
        if (pago.Documentos.Count == 0 || pago.Documentos.Any(d => d.EquivalenciaDR <= 0))
            throw new InvalidOperationException("El pago necesita documentos con equivalencia positiva.");

        var impuestos = pago.Documentos.SelectMany(d => d.Impuestos.Select(i => new ImpuestoDePagoCalculado(
            i.Impuesto, i.TipoFactor, i.TasaOCuota, i.Base / d.EquivalenciaDR,
            i.Importe / d.EquivalenciaDR, i.EsRetencion))).ToArray();

        var retenciones = impuestos.Where(i => i.EsRetencion).GroupBy(i => i.Impuesto)
            .Select(g => new ImpuestoDePagoCalculado(g.Key, TiposDeFactor.Tasa, null, 0m,
                RedondearImpuesto(g.Sum(i => i.Importe ?? 0m)), true));
        var traslados = impuestos.Where(i => !i.EsRetencion)
            .GroupBy(i => (i.Impuesto, i.TipoFactor, i.TasaOCuota))
            .Select(g => new ImpuestoDePagoCalculado(g.Key.Impuesto, g.Key.TipoFactor, g.Key.TasaOCuota,
                RedondearImpuesto(g.Sum(i => i.Base)),
                g.Key.TipoFactor == TiposDeFactor.Exento ? null : RedondearImpuesto(g.Sum(i => i.Importe ?? 0m)), false));
        return retenciones.Concat(traslados).OrderBy(i => i.Impuesto)
            .ThenBy(i => i.TipoFactor).ThenBy(i => i.TasaOCuota).ToArray();
    }

    private static decimal RedondearImpuesto(decimal importe)
        => Math.Round(importe, 6, MotorDeImpuestos.ModoDeRedondeo);

    private static XElement NodoDePago(Pago pago, int d, TimeZoneInfo zona,
        IReadOnlyList<ImpuestoDePagoCalculado> impuestos)
    {
        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(pago.FechaPagoUtc, DateTimeKind.Utc), zona);
        var nodo = new XElement(Pago20 + "Pago",
            new XAttribute("FechaPago", fechaLocal.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)),
            new XAttribute("FormaDePagoP", pago.FormaDePagoP),
            new XAttribute("MonedaP", pago.MonedaP),
            new XAttribute("TipoCambioP", Moneda(CambioAMxn(pago), DecimalesDeTasa)),
            new XAttribute("Monto", Moneda(pago.Monto, d)),
            Opcional("NumOperacion", pago.NumOperacion),
            Opcional("RfcEmisorCtaOrd", pago.RfcEmisorCtaOrd),
            Opcional("NomBancoOrdExt", pago.NomBancoOrdExt),
            Opcional("CtaOrdenante", pago.CtaOrdenante),
            Opcional("RfcEmisorCtaBen", pago.RfcEmisorCtaBen),
            Opcional("CtaBeneficiario", pago.CtaBeneficiario));

        foreach (var documento in pago.Documentos.OrderBy(x => x.NumParcialidad).ThenBy(x => x.IdDocumento))
            nodo.Add(NodoDeDocumento(documento, d));

        if (impuestos.Count > 0)
        {
            var retenciones = impuestos.Where(i => i.EsRetencion).ToArray();
            var traslados = impuestos.Where(i => !i.EsRetencion).ToArray();
            nodo.Add(new XElement(Pago20 + "ImpuestosP",
                retenciones.Length == 0 ? null : new XElement(Pago20 + "RetencionesP",
                    retenciones.Select(i => new XElement(Pago20 + "RetencionP",
                        new XAttribute("ImpuestoP", i.Impuesto), new XAttribute("ImporteP", Moneda(i.Importe ?? 0m, 6))))),
                traslados.Length == 0 ? null : new XElement(Pago20 + "TrasladosP",
                    traslados.Select(i => new XElement(Pago20 + "TrasladoP",
                        new XAttribute("BaseP", Moneda(i.Base, 6)), new XAttribute("ImpuestoP", i.Impuesto),
                        new XAttribute("TipoFactorP", i.TipoFactor),
                        i.TipoFactor == TiposDeFactor.Exento ? null : new XAttribute("TasaOCuotaP", Moneda(i.TasaOCuota ?? 0m, 6)),
                        i.TipoFactor == TiposDeFactor.Exento ? null : new XAttribute("ImporteP", Moneda(i.Importe ?? 0m, 6)))))));
        }

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
                    new XAttribute("BaseDR", Moneda(i.Base, 6)),
                    new XAttribute("ImpuestoDR", i.Impuesto),
                    new XAttribute("TipoFactorDR", i.TipoFactor),
                    new XAttribute("TasaOCuotaDR", Moneda(i.TasaOCuota ?? 0m, DecimalesDeTasa)),
                    new XAttribute("ImporteDR", Moneda(i.Importe ?? 0m, 6))))));
        }

        if (traslados.Length > 0)
        {
            impuestos.Add(new XElement(Pago20 + "TrasladosDR", traslados.Select(i =>
                new XElement(Pago20 + "TrasladoDR",
                    new XAttribute("BaseDR", Moneda(i.Base, 6)),
                    new XAttribute("ImpuestoDR", i.Impuesto),
                    new XAttribute("TipoFactorDR", i.TipoFactor),
                    // Un exento no lleva tasa ni importe; declararlos en cero es rechazo.
                    i.TipoFactor == TiposDeFactor.Exento
                        ? null
                        : new XAttribute("TasaOCuotaDR", Moneda(i.TasaOCuota ?? 0m, DecimalesDeTasa)),
                    i.TipoFactor == TiposDeFactor.Exento
                        ? null
                        : new XAttribute("ImporteDR", Moneda(i.Importe ?? 0m, 6))))));
        }

        nodo.Add(impuestos);

        return nodo;
    }

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
