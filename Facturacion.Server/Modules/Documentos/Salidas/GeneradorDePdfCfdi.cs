using System.Globalization;
using Facturacion.Server.Data.Entidades.Documentos;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>Lo que el PDF necesita y no está dentro del comprobante.</summary>
/// <param name="Logo">Logo de la empresa, ya descifrado del almacén. Nulo si no tiene.</param>
public sealed record DatosDelPdf(DateTime FechaLocal, int Decimales, byte[]? Logo);

/// <summary>
/// Representación impresa del CFDI. Los campos son los que fija §1.4 del documento
/// funcional, que ahí está como contrato del reporte y no como sugerencia de diseño.
///
/// <para><b>Esto no es la factura</b></para>
/// La factura es el XML timbrado; este PDF es su representación impresa. Por eso lleva el QR
/// de verificación, la cadena original del complemento de certificación y los dos sellos: son
/// lo que permite a quien la recibe comprobar contra el SAT que el documento es real, sin
/// confiar en el papel.
///
/// <para><b>Todo sale del comprobante</b></para>
/// Ni una consulta al cliente ni al catálogo de productos: los datos están congelados dentro
/// (ARQUITECTURA.md §5). Reimprimir una factura de hace dos años tiene que dar el mismo papel.
/// </summary>
public sealed class GeneradorDePdfCfdi
{
    private static readonly string[] EncabezadosConceptos =
        ["CANTIDAD", "CLAVE UNIDAD", "CLAVE DEL PRODUCTO", "DESCRIPCIÓN", "OBJ. IMP.", "P. UNITARIO", "IMPORTE"];

    /*
    public byte[] Generar(Comprobante comprobante, DatosDelPdf datos)
    {
        var qr = Qr(comprobante, datos.Decimales);

        return Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.Letter);
                pagina.Margin(1.2f, Unit.Centimetre);
                pagina.DefaultTextStyle(t => t.FontSize(8).FontFamily(Fonts.Calibri));

                pagina.Header().Element(e => Encabezado(e, comprobante, datos));
                pagina.Content().Element(e => Cuerpo(e, comprobante, datos));
                pagina.Footer().Element(e => Pie(e, comprobante, qr));
            });
        }).GeneratePdf();
    }
    */

    public byte[] Generar(Comprobante comprobante, DatosDelPdf datos)
        => Construir(comprobante, datos).GeneratePdf();

    public IDocument Construir(Comprobante comprobante, DatosDelPdf datos)
    {
        var qr = Qr(comprobante, datos.Decimales);

        return Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.Letter);
                pagina.Margin(1.2f, Unit.Centimetre);
                pagina.DefaultTextStyle(t => t.FontSize(8).FontFamily(Fonts.Calibri));

                pagina.Header().Element(e => Encabezado(e, comprobante, datos));
                pagina.Content().Element(e => Cuerpo(e, comprobante, datos));
                pagina.Footer().Element(e => Pie(e, comprobante, qr));
            });
        });
    }

    // ── Encabezado: emisor, logo y los identificadores fiscales ─────────────────────────

    private static void Encabezado(IContainer contenedor, Comprobante c, DatosDelPdf datos)
        => contenedor.PaddingBottom(8).Row(fila =>
        {
            if (datos.Logo is { Length: > 0 } logo)
                fila.ConstantItem(90).Height(45).AlignLeft().AlignMiddle().Image(logo).FitArea();

            fila.RelativeItem().PaddingLeft(8).Column(columna =>
            {
                columna.Item().Text(c.EmisorNombre).Bold().FontSize(12);
                columna.Item().Text($"RFC {c.EmisorRfc}   ·   Régimen fiscal {c.EmisorRegimenFiscal}");
                columna.Item().Text($"Lugar de expedición {c.LugarExpedicion}");
            });

            fila.ConstantItem(210).Border(0.5f).Padding(5).Column(columna =>
            {
                columna.Item().Text(TituloDelTipo(c.TipoDeComprobante)).Bold().FontSize(11).AlignCenter();
                columna.Item().PaddingTop(3).Element(e => Dato(e, "Versión", "4.0"));
                columna.Item().Element(e => Dato(e, "Folio interno", FolioInterno(c)));
                columna.Item().Element(e => Dato(e, "Folio fiscal (UUID)", c.Uuid?.ToString().ToUpperInvariant() ?? "—"));
                columna.Item().Element(e => Dato(e, "No. serie CSD emisor", c.NoCertificadoEmisor ?? "—"));
                columna.Item().Element(e => Dato(e, "No. serie CSD SAT", c.NoCertificadoSat ?? "—"));
                columna.Item().Element(e => Dato(e, "Fecha de emisión", datos.FechaLocal.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)));
                columna.Item().Element(e => Dato(e, "Fecha de certificación", c.FechaTimbradoUtc?.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture) ?? "—"));
            });
        });

    // ── Cuerpo: receptor, conceptos y totales ───────────────────────────────────────────

    private static void Cuerpo(IContainer contenedor, Comprobante c, DatosDelPdf datos)
        => contenedor.Column(columna =>
        {
            columna.Item().Border(0.5f).Padding(5).Column(receptor =>
            {
                receptor.Item().Text("RECEPTOR").Bold();
                receptor.Item().Text(c.ReceptorNombre).Bold();
                receptor.Item().Text($"RFC {c.ReceptorRfc}   ·   Régimen fiscal {c.ReceptorRegimenFiscal}");
                receptor.Item().Text($"Domicilio fiscal {c.ReceptorDomicilioFiscal}   ·   Uso del CFDI {c.ReceptorUsoCfdi}");
            });

            columna.Item().PaddingTop(6).Element(e => Conceptos(e, c, datos.Decimales));
            columna.Item().PaddingTop(6).Element(e => Totales(e, c, datos.Decimales));
        });

    private static void Conceptos(IContainer contenedor, Comprobante c, int decimales)
        => contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(columnas =>
            {
                columnas.ConstantColumn(45);   // cantidad
                columnas.ConstantColumn(55);   // clave unidad
                columnas.ConstantColumn(60);   // clave producto
                columnas.RelativeColumn();     // descripción
                columnas.ConstantColumn(40);   // objeto impuesto
                columnas.ConstantColumn(60);   // precio unitario
                columnas.ConstantColumn(60);   // importe
            });

            tabla.Header(encabezado =>
            {
                foreach (var titulo in EncabezadosConceptos)
                    encabezado.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(titulo).Bold().FontSize(7);
            });

            foreach (var concepto in c.Conceptos.OrderBy(x => x.Orden))
            {
                tabla.Cell().Padding(3).AlignRight().Text(Cifra(concepto.Cantidad, 6));
                tabla.Cell().Padding(3).Text(concepto.ClaveUnidad);
                tabla.Cell().Padding(3).Text(concepto.ClaveProdServ);
                tabla.Cell().Padding(3).Text(concepto.Descripcion);
                tabla.Cell().Padding(3).AlignCenter().Text(concepto.ObjetoImp);
                tabla.Cell().Padding(3).AlignRight().Text(Cifra(concepto.ValorUnitario, decimales));
                tabla.Cell().Padding(3).AlignRight().Text(Cifra(concepto.Importe, decimales));

                if (concepto.Impuestos.Count == 0) continue;

                // Sub-tabla de impuestos del renglón: es lo que permite a quien recibe la
                // factura reconstruir de dónde salió cada peso de impuesto.
                tabla.Cell().ColumnSpan(7).PaddingLeft(45).PaddingBottom(3).Table(sub =>
                {
                    sub.ColumnsDefinition(columnas =>
                    {
                        columnas.ConstantColumn(70);
                        columnas.ConstantColumn(60);
                        columnas.ConstantColumn(60);
                        columnas.ConstantColumn(70);
                        columnas.ConstantColumn(70);
                    });

                    foreach (var titulo in new[] { "BASE", "IMPUESTO", "TIPO FACTOR", "TASA O CUOTA", "IMPORTE" })
                        sub.Cell().Padding(2).Text(titulo).FontSize(6).Light();

                    foreach (var impuesto in concepto.Impuestos)
                    {
                        sub.Cell().Padding(2).Text(Cifra(impuesto.Base, decimales)).FontSize(7);
                        sub.Cell().Padding(2).Text(NombreDeImpuesto(impuesto.Impuesto, impuesto.EsRetencion)).FontSize(7);
                        sub.Cell().Padding(2).Text(impuesto.TipoFactor).FontSize(7);
                        // Exento: sin tasa y sin importe, igual que en el XML.
                        sub.Cell().Padding(2).Text(impuesto.TasaOCuota is { } t ? Cifra(t, 6) : "—").FontSize(7);
                        sub.Cell().Padding(2).Text(impuesto.Importe is { } i ? Cifra(i, decimales) : "—").FontSize(7);
                    }
                });
            }
        });

    private static void Totales(IContainer contenedor, Comprobante c, int decimales)
        => contenedor.Row(fila =>
        {
            fila.RelativeItem().Column(pago =>
            {
                pago.Item().Text($"Método de pago: {c.MetodoPago ?? "—"}");
                pago.Item().Text($"Forma de pago: {c.FormaPago ?? "—"}");
                pago.Item().Text($"Moneda: {c.Moneda}");
                pago.Item().Text($"Tipo de cambio: {(c.TipoCambio is { } tc ? Cifra(tc, 6) : "—")}");
            });

            fila.ConstantItem(230).Column(totales =>
            {
                totales.Item().Element(e => Dato(e, "Sub-total", Cifra(c.SubTotal, decimales)));

                if (c.Descuento > 0)
                    totales.Item().Element(e => Dato(e, "Descuento", Cifra(c.Descuento, decimales)));

                if (c.TotalImpuestosTrasladados > 0)
                    totales.Item().Element(e => Dato(e, "Impuestos trasladados", Cifra(c.TotalImpuestosTrasladados, decimales)));

                if (c.TotalImpuestosRetenidos > 0)
                    totales.Item().Element(e => Dato(e, "Impuestos retenidos", Cifra(c.TotalImpuestosRetenidos, decimales)));

                totales.Item().PaddingTop(2).BorderTop(0.5f).Element(e => Dato(e, "Total", Cifra(c.Total, decimales), negrita: true));
            });
        });

    // ── Pie: QR, sellos y cadena original ───────────────────────────────────────────────

    private static void Pie(IContainer contenedor, Comprobante c, byte[]? qr)
        => contenedor.PaddingTop(6).BorderTop(0.5f).PaddingTop(4).Row(fila =>
        {
            if (qr is not null)
                fila.ConstantItem(85).Height(85).Image(qr).FitArea();

            fila.RelativeItem().PaddingLeft(6).Column(columna =>
            {
                columna.Item().Element(e => Bloque(e, "Sello digital del CFDI", c.SelloCfd));
                columna.Item().PaddingTop(2).Element(e => Bloque(e, "Sello del SAT", c.SelloSat));
                columna.Item().PaddingTop(2).Element(e => Bloque(e, "Cadena original del complemento de certificación", c.CadenaOriginalSat));

                columna.Item().PaddingTop(3).Text(
                    "Este documento es una representación impresa de un CFDI. " +
                    "Verifícalo en la página del SAT con el código QR.").FontSize(6).Light();
            });
        });

    /// <summary>
    /// El QR solo se dibuja si la expresión se puede armar. Un comprobante sin timbrar no la
    /// tiene, y un QR que lleva a una página de error del SAT es peor que no ponerlo.
    /// </summary>
    private static byte[]? Qr(Comprobante c, int decimales)
    {
        var expresion = ExpresionImpresa.Construir(
            c.Uuid, c.EmisorRfc, c.ReceptorRfc, ExpresionImpresa.Total(c.Total, decimales), c.SelloCfd);

        if (expresion is null) return null;

        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(expresion, QRCodeGenerator.ECCLevel.M);
        using var png = new PngByteQRCode(datos);

        return png.GetGraphic(20);
    }

    // ── Piezas comunes ──────────────────────────────────────────────────────────────────

    private static void Dato(IContainer contenedor, string etiqueta, string valor, bool negrita = false)
        => contenedor.Row(fila =>
        {
            fila.RelativeItem().Text(etiqueta).FontSize(7).Light();

            var texto = fila.ConstantItem(120).AlignRight().Text(valor).FontSize(7);
            if (negrita) texto.Bold();
        });

    private static void Bloque(IContainer contenedor, string etiqueta, string? valor)
        => contenedor.Column(columna =>
        {
            columna.Item().Text(etiqueta).FontSize(6).Light();
            columna.Item().Text(valor ?? "—").FontSize(5).FontFamily(Fonts.Consolas);
        });

    private static string Cifra(decimal valor, int decimales)
        => valor.ToString("N" + decimales.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    private static string FolioInterno(Comprobante c)
        => c.Folio is null ? "—" : $"{c.Serie}{c.Folio}";

    private static string NombreDeImpuesto(string clave, bool esRetencion)
    {
        var nombre = clave switch
        {
            "001" => "ISR",
            "002" => "IVA",
            "003" => "IEPS",
            _ => clave
        };

        return esRetencion ? $"{nombre} ret." : nombre;
    }

    private static string TituloDelTipo(string tipo) => tipo switch
    {
        "I" => "FACTURA",
        "E" => "NOTA DE CRÉDITO",
        "T" => "TRASLADO",
        "N" => "NÓMINA",
        "P" => "COMPLEMENTO DE PAGO",
        _ => "COMPROBANTE"
    };
}
