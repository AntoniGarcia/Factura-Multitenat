using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Facturacion.Server.Modules.Plataforma.Timbres;

/// <summary>Datos históricos que se imprimen en el comprobante interno de una compra.</summary>
public sealed record DatosDeComprobanteDeCompra(
    Guid CompraId,
    string NombreDelSistema,
    string EmpresaNombre,
    string EmpresaRfc,
    string Concepto,
    int CantidadTimbres,
    decimal PrecioPorTimbre,
    decimal Subtotal,
    decimal Iva,
    decimal TasaIva,
    decimal Total,
    DateTime FechaDeCompraUtc,
    DateTime FechaDePagoUtc,
    DateTime? VenceUtc);

/// <summary>
/// Genera el comprobante comercial de una compra pagada. No es una representación impresa
/// de CFDI: mientras no existan emisor fiscal, impuestos y timbrado, no lleva elementos que
/// puedan hacerlo pasar por una factura válida.
/// </summary>
public sealed class GeneradorDeComprobanteDeCompraPdf
{
    private const string AzulTinta = "#17243A";
    private const string AzulAcento = "#245BDB";
    private const string TextoSecundario = "#64748B";
    private const string Borde = "#DCE3EC";
    private const string FondoExito = "#DCFCE7";
    private const string TextoExito = "#15803D";
    private const string TextoAviso = "#B42318";

    public byte[] Generar(DatosDeComprobanteDeCompra datos)
        => Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(PageSizes.Letter);
            pagina.MarginHorizontal(1.8f, Unit.Centimetre);
            pagina.MarginVertical(1.5f, Unit.Centimetre);
            pagina.DefaultTextStyle(t => t.FontSize(9).FontFamily(Fonts.Calibri));

            pagina.Header().Element(e => Encabezado(e, datos));
            pagina.Content().Element(e => Cuerpo(e, datos));
            pagina.Footer().AlignCenter().Text(texto =>
            {
                texto.Span("Folio interno completo: ").FontSize(7).FontColor(TextoSecundario);
                texto.Span(datos.CompraId.ToString().ToUpperInvariant())
                    .FontSize(7).FontFamily(Fonts.Consolas).FontColor(TextoSecundario);
            });
        })).GeneratePdf();

    private static void Encabezado(IContainer contenedor, DatosDeComprobanteDeCompra datos)
        => contenedor.PaddingBottom(16).Column(columna =>
        {
            columna.Item().Row(fila =>
            {
                fila.RelativeItem().Column(identidad =>
                {
                    identidad.Item().Text(datos.NombreDelSistema).FontSize(15).Bold().FontColor(AzulTinta);
                    identidad.Item().PaddingTop(3).Text("Servicios de facturación electrónica")
                        .FontSize(8).FontColor(TextoSecundario);
                });

                fila.ConstantItem(235).AlignRight().Column(titulo =>
                {
                    titulo.Item().AlignRight().Text("COMPROBANTE DE COMPRA")
                        .FontSize(16).Bold().FontColor(AzulAcento);
                    titulo.Item().PaddingTop(2).AlignRight().Text($"Folio {FolioCorto(datos.CompraId)}")
                        .FontSize(10).Bold().FontFamily(Fonts.Consolas).FontColor(AzulTinta);
                    titulo.Item().PaddingTop(5).AlignRight().Row(estado =>
                    {
                        estado.RelativeItem();
                        estado.AutoItem().Background(FondoExito).PaddingHorizontal(8).PaddingVertical(3)
                            .Text("Pago acreditado").FontSize(7).SemiBold().FontColor(TextoExito);
                    });
                });
            });

            columna.Item().PaddingTop(15).BorderBottom(1).BorderColor(AzulTinta);
            columna.Item().PaddingTop(9).AlignRight().Text("DOCUMENTO INTERNO - SIN VALIDEZ FISCAL")
                .FontSize(7).Bold().FontColor(TextoAviso);
        });

    private static void Cuerpo(IContainer contenedor, DatosDeComprobanteDeCompra datos)
        => contenedor.Column(columna =>
        {
            columna.Item().PaddingTop(2).Row(fila =>
            {
                fila.RelativeItem().Element(e => BloqueDeCliente(e, datos));
                fila.RelativeItem().PaddingLeft(28).Element(e => BloqueDeCompra(e, datos));
            });

            columna.Item().PaddingTop(22).Element(e => Concepto(e, datos));
            columna.Item().PaddingTop(12).AlignRight().Width(255).Element(e => Total(e, datos));

            columna.Item().PaddingTop(25).BorderTop(0.7f).BorderColor(Borde).PaddingTop(12).Row(fila =>
            {
                fila.ConstantItem(4).Height(47).Background(AzulAcento);
                fila.RelativeItem().PaddingLeft(10).Column(aviso =>
                {
                    aviso.Item().Text("COMPROBANTE INTERNO").FontSize(7).Bold().FontColor(TextoSecundario);
                    aviso.Item().PaddingTop(3).Text(
                            "Este documento acredita la compra y la entrega de timbres dentro de la plataforma. " +
                            "No es un CFDI y no contiene UUID, sellos digitales, código QR ni cadena original.")
                        .FontSize(8).FontColor(TextoSecundario);
                });
            });
        });

    private static void BloqueDeCliente(IContainer contenedor, DatosDeComprobanteDeCompra datos)
        => contenedor.Column(columna =>
        {
            columna.Item().Text("EMPRESA COMPRADORA").FontSize(7).Bold().FontColor(TextoSecundario);
            columna.Item().PaddingTop(5).Text(datos.EmpresaNombre).FontSize(10).Bold().FontColor(AzulTinta);
            columna.Item().PaddingTop(2).Text($"RFC: {datos.EmpresaRfc}")
                .FontSize(8).FontFamily(Fonts.Consolas).FontColor(TextoSecundario);
        });

    private static void BloqueDeCompra(IContainer contenedor, DatosDeComprobanteDeCompra datos)
        => contenedor.Column(columna =>
        {
            columna.Item().Text("DATOS DE LA COMPRA").FontSize(7).Bold().FontColor(TextoSecundario);
            columna.Item().PaddingTop(5).Element(e => Dato(e, "Solicitada", Fecha(datos.FechaDeCompraUtc)));
            columna.Item().PaddingTop(3).Element(e => Dato(e, "Acreditada", Fecha(datos.FechaDePagoUtc)));
            columna.Item().PaddingTop(3).Element(e => Dato(
                e, "Vigencia", datos.VenceUtc is { } vence ? Fecha(vence) : "No especificada"));
        });

    private static void Concepto(IContainer contenedor, DatosDeComprobanteDeCompra datos)
        => contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(columnas =>
            {
                columnas.ConstantColumn(78);
                columnas.RelativeColumn();
                columnas.ConstantColumn(100);
                columnas.ConstantColumn(100);
            });

            tabla.Header(encabezado =>
            {
                EncabezadoDeCelda(encabezado.Cell(), "Cantidad");
                EncabezadoDeCelda(encabezado.Cell(), "Descripción");
                EncabezadoDeCelda(encabezado.Cell(), "P. unitario");
                EncabezadoDeCelda(encabezado.Cell(), "Importe");
            });

            Celda(tabla.Cell().AlignRight(), datos.CantidadTimbres.ToString("N0", Cultura));
            Celda(tabla.Cell(), datos.Concepto);
            Celda(tabla.Cell().AlignRight(), Moneda(datos.PrecioPorTimbre));
            Celda(tabla.Cell().AlignRight(), Moneda(datos.Total));
        });

    private static void Total(IContainer contenedor, DatosDeComprobanteDeCompra datos)
        => contenedor.Column(columna =>
        {
            columna.Item().Element(e => RenglonTotal(e, "Subtotal", datos.Subtotal));
            columna.Item().PaddingTop(4).Element(e => RenglonTotal(
                e, $"IVA {datos.TasaIva:P0}", datos.Iva));
            columna.Item().PaddingTop(7).BorderTop(0.7f).BorderColor(Borde)
                .PaddingTop(7).Row(fila =>
                {
                    fila.RelativeItem().Text("Total MXN").FontSize(11).Bold().FontColor(AzulTinta);
                    fila.ConstantItem(110).AlignRight().Text(Moneda(datos.Total))
                        .FontSize(13).Bold().FontColor(AzulAcento);
                });
        });

    private static void RenglonTotal(IContainer contenedor, string etiqueta, decimal importe)
        => contenedor.Row(fila =>
        {
            fila.RelativeItem().Text(etiqueta);
            fila.ConstantItem(110).AlignRight().Text(Moneda(importe));
        });

    private static void EncabezadoDeCelda(IContainer celda, string texto)
        => celda.Background(AzulTinta).PaddingVertical(7).PaddingHorizontal(8)
            .Text(texto).FontSize(7).FontColor(Colors.White).SemiBold();

    private static void Celda(IContainer celda, string texto)
        => celda.BorderBottom(0.5f).BorderColor(Borde).PaddingVertical(9).PaddingHorizontal(8).Text(texto);

    private static void Dato(IContainer contenedor, string etiqueta, string valor)
        => contenedor.Row(fila =>
        {
            fila.RelativeItem().Text(etiqueta).FontSize(8).FontColor(TextoSecundario);
            fila.ConstantItem(125).AlignRight().Text(valor).FontSize(8)
                .FontFamily(Fonts.Consolas).FontColor(AzulTinta);
        });

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-MX");

    private static string Moneda(decimal importe) => importe.ToString("C2", Cultura);

    private static string Fecha(DateTime utc)
        => utc.ToString("dd/MM/yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string FolioCorto(Guid id) => id.ToString("N")[..12].ToUpperInvariant();
}
