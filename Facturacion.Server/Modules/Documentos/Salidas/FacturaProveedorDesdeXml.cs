using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>El PDF lee la copia emitida del XML; no consulta datos actuales de clientes ni del proveedor.</summary>
public static class FacturaProveedorDesdeXml
{
    public static (Comprobante Comprobante, DatosDelPdf Datos) Leer(FacturaProveedorEmitidaDto factura)
    {
        using var lector = XmlReader.Create(new StringReader(factura.XmlTimbrado), new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 120_000 });
        var doc = XDocument.Load(lector);
        XNamespace cfdi = EsquemasSat.EspacioDeNombresCfdi;
        XNamespace tfd = "http://www.sat.gob.mx/TimbreFiscalDigital";
        var raiz = doc.Root ?? throw Invalido();
        if (raiz.Name != cfdi + "Comprobante" || Texto(raiz, "Version") != "4.0" ||
            Texto(raiz, "TipoDeComprobante") != "I" || Texto(raiz, "Moneda") != "MXN" ||
            Texto(raiz, "MetodoPago") != "PUE") throw Invalido();
        var emisor = Unico(raiz, cfdi + "Emisor");
        var receptor = Unico(raiz, cfdi + "Receptor");
        var timbre = Unico(Unico(raiz, cfdi + "Complemento"), tfd + "TimbreFiscalDigital");
        if (!Guid.TryParse(Texto(timbre, "UUID"), out var uuid) || uuid != factura.Uuid ||
            Texto(timbre, "SelloCFD") != Texto(raiz, "Sello")) throw Invalido();
        var zona = TimeZoneInfo.FindSystemTimeZoneById(factura.ZonaHoraria);
        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(factura.FechaEmisionUtc, DateTimeKind.Utc), zona);
        if (!DateTime.TryParseExact(Texto(raiz, "Fecha"), "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var fechaXml) || fechaXml != fechaLocal.AddTicks(-(fechaLocal.Ticks % TimeSpan.TicksPerSecond)))
            throw Invalido();
        if (!int.TryParse(Texto(raiz, "Folio"), NumberStyles.None, CultureInfo.InvariantCulture, out var folio) || folio <= 0)
            throw Invalido();
        var c = new Comprobante
        {
            Id = factura.FacturaId, Estatus = "timbrado", Uuid = uuid, TipoDeComprobante = "I",
            Serie = Texto(raiz, "Serie"), Folio = folio,
            FechaEmisionUtc = factura.FechaEmisionUtc, FechaTimbradoUtc = factura.FechaTimbradoUtc,
            LugarExpedicion = Texto(raiz, "LugarExpedicion"), Moneda = "MXN", MetodoPago = "PUE",
            FormaPago = Texto(raiz, "FormaPago"), Exportacion = Texto(raiz, "Exportacion"),
            EmisorRfc = Texto(emisor, "Rfc"), EmisorNombre = Texto(emisor, "Nombre"), EmisorRegimenFiscal = Texto(emisor, "RegimenFiscal"),
            ReceptorRfc = Texto(receptor, "Rfc"), ReceptorNombre = Texto(receptor, "Nombre"),
            ReceptorRegimenFiscal = Texto(receptor, "RegimenFiscalReceptor"), ReceptorDomicilioFiscal = Texto(receptor, "DomicilioFiscalReceptor"),
            ReceptorUsoCfdi = Texto(receptor, "UsoCFDI"), SubTotal = Numero(raiz, "SubTotal"), Total = Numero(raiz, "Total"),
            NoCertificadoEmisor = Texto(raiz, "NoCertificado"), SelloCfd = Texto(raiz, "Sello"),
            NoCertificadoSat = Texto(timbre, "NoCertificadoSAT"), SelloSat = Texto(timbre, "SelloSAT"), CadenaOriginalSat = factura.CadenaOriginalSat
        };
        var conceptos = Unico(raiz, cfdi + "Conceptos").Elements(cfdi + "Concepto").ToList();
        if (conceptos.Count != 1) throw Invalido();
        var concepto = conceptos[0];
        var impuesto = Unico(Unico(Unico(concepto, cfdi + "Impuestos"), cfdi + "Traslados"), cfdi + "Traslado");
        var traslado = Unico(Unico(Unico(raiz, cfdi + "Impuestos"), cfdi + "Traslados"), cfdi + "Traslado");
        c.TotalImpuestosTrasladados = Numero(Unico(raiz, cfdi + "Impuestos"), "TotalImpuestosTrasladados");
        c.Conceptos.Add(new Concepto
        {
            Orden = 1, Cantidad = Numero(concepto, "Cantidad"), ValorUnitario = Numero(concepto, "ValorUnitario"),
            Importe = Numero(concepto, "Importe"), ClaveProdServ = Texto(concepto, "ClaveProdServ"), ClaveUnidad = Texto(concepto, "ClaveUnidad"),
            Descripcion = Texto(concepto, "Descripcion"), ObjetoImp = Texto(concepto, "ObjetoImp"),
            Impuestos = [new ImpuestoConcepto { Impuesto = Texto(impuesto, "Impuesto"), TipoFactor = Texto(impuesto, "TipoFactor"),
                TasaOCuota = Numero(impuesto, "TasaOCuota"), Base = Numero(impuesto, "Base"), Importe = Numero(impuesto, "Importe") }]
        });
        if (c.Total != c.SubTotal + c.TotalImpuestosTrasladados || c.Conceptos[0].Importe != c.SubTotal ||
            c.Conceptos[0].Cantidad != 1 || c.Conceptos[0].ValorUnitario != c.SubTotal ||
            Numero(impuesto, "Importe") != c.TotalImpuestosTrasladados || Numero(traslado, "Importe") != c.TotalImpuestosTrasladados)
            throw Invalido();
        return (c, new DatosDelPdf(fechaLocal, 2, null,
            LeyendaDelTimbre: (string?)timbre.Attribute("Leyenda")) { ZonaHoraria = zona });
    }

    private static XElement Unico(XElement padre, XName nombre)
    {
        var elementos = padre.Elements(nombre).Take(2).ToList();
        return elementos.Count == 1 ? elementos[0] : throw Invalido();
    }
    private static string Texto(XElement e, string atributo)
        => e.Attribute(atributo)?.Value is { Length: > 0 } valor ? valor : throw Invalido();
    private static decimal Numero(XElement e, string atributo)
        => decimal.TryParse(Texto(e, atributo), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var n) && n >= 0
            ? n : throw Invalido();
    private static InvalidDataException Invalido() => new("El XML de la factura de compra está incompleto o no coincide con su emisión.");
}
