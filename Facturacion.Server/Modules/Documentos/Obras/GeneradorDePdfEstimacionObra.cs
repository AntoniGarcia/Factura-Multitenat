using System.Globalization;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.Obras;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Facturacion.Server.Modules.Documentos.Obras;

public sealed class GeneradorDePdfEstimacionObra
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-MX");

    public byte[] Generar(Comprobante factura, DatosObraDto obra, DateTime fechaLocal)
        => Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.Letter);
                pagina.Margin(1.5f, Unit.Centimetre);
                pagina.DefaultTextStyle(texto => texto.FontFamily(Fonts.Calibri).FontSize(9));

                pagina.Header().Column(encabezado =>
                {
                    encabezado.Item().Text("ESTIMACIÓN DE OBRA").Bold().FontSize(17);
                    encabezado.Item().PaddingTop(3).Text("BORRADOR - DOCUMENTO INTERNO SIN VALIDEZ FISCAL")
                        .Bold().FontSize(9);
                    encabezado.Item().PaddingTop(7).LineHorizontal(1);
                });

                pagina.Content().PaddingTop(12).Column(cuerpo =>
                {
                    cuerpo.Spacing(12);
                    cuerpo.Item().Row(fila =>
                    {
                        fila.RelativeItem().Column(emisor =>
                        {
                            emisor.Item().Text("EMPRESA").Bold().FontSize(8);
                            emisor.Item().Text(factura.EmisorNombre).Bold();
                            emisor.Item().Text($"RFC {factura.EmisorRfc}");
                        });
                        fila.RelativeItem().Column(receptor =>
                        {
                            receptor.Item().Text("CLIENTE").Bold().FontSize(8);
                            receptor.Item().Text(factura.ReceptorNombre).Bold();
                            receptor.Item().Text($"RFC {factura.ReceptorRfc}");
                            receptor.Item().Text($"Fecha: {fechaLocal:dd/MM/yyyy}");
                        });
                    });

                    cuerpo.Item().Column(seccion =>
                    {
                        seccion.Item().Text("TRABAJOS CAPTURADOS").Bold().FontSize(9);
                        seccion.Item().PaddingTop(4).Table(tabla =>
                        {
                            tabla.ColumnsDefinition(columnas =>
                            {
                                columnas.ConstantColumn(48);
                                columnas.RelativeColumn();
                                columnas.ConstantColumn(80);
                                columnas.ConstantColumn(88);
                            });
                            tabla.Header(cabecera =>
                            {
                                Cabecera(cabecera, "Cantidad");
                                Cabecera(cabecera, "Descripción");
                                Cabecera(cabecera, "P. unitario");
                                Cabecera(cabecera, "Importe");
                            });
                            foreach (var concepto in factura.Conceptos.OrderBy(x => x.Orden))
                            {
                                Celda(tabla, concepto.Cantidad.ToString("N2", Cultura), true);
                                Celda(tabla, concepto.Descripcion);
                                Celda(tabla, Dinero(concepto.ValorUnitario), true);
                                Celda(tabla, Dinero(concepto.Importe), true);
                            }
                        });
                    });

                    cuerpo.Item().Row(fila =>
                    {
                        fila.RelativeItem();
                        fila.ConstantItem(285).Column(desglose =>
                        {
                            desglose.Item().Text("CÁLCULO DE LA ESTIMACIÓN").Bold().FontSize(9);
                            Renglon(desglose, "Importe de los trabajos", obra.ImporteTrabajos);
                            Renglon(desglose, $"Amortización del anticipo ({obra.PorcentajeAmortizacion:N2} %)", -obra.Amortizacion);
                            Renglon(desglose, "Retenciones", -obra.Retenciones);
                            Renglon(desglose, "Devoluciones", -obra.Devoluciones);
                            Renglon(desglose, "Subtotal de la estimación", obra.SubtotalEstimacion, true);
                            Renglon(desglose, $"IVA estimado ({obra.PorcentajeIva:N2} %)", obra.IvaEstimado);
                            Renglon(desglose, "Total de la estimación", obra.TotalEstimacion, true);
                            foreach (var deduccion in obra.Deducciones)
                                Renglon(desglose, $"{deduccion.Nombre} ({deduccion.Porcentaje:N2} %)", -deduccion.Importe);
                            desglose.Item().PaddingTop(5).LineHorizontal(1);
                            Renglon(desglose, "Importe líquido estimado", obra.ImporteLiquido, true);
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(factura.Observaciones))
                        cuerpo.Item().Column(observaciones =>
                        {
                            observaciones.Item().Text("OBSERVACIONES").Bold().FontSize(8);
                            observaciones.Item().Text(factura.Observaciones);
                        });
                });

                pagina.Footer().Column(pie =>
                {
                    pie.Item().LineHorizontal(0.5f);
                    pie.Item().PaddingTop(4).Text(
                        "Estimación comercial para revisión. No es CFDI, no tiene folio fiscal ni acredita impuestos. " +
                        "Los importes del CFDI se determinarán por separado.").FontSize(7);
                });
            });
        }).GeneratePdf();

    private static void Cabecera(TableCellDescriptor celda, string texto)
        => celda.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(texto).Bold().FontSize(8);

    private static void Celda(TableDescriptor tabla, string texto, bool derecha = false)
    {
        var celda = tabla.Cell().BorderBottom(0.3f).Padding(4);
        if (derecha) celda.AlignRight().Text(texto);
        else celda.Text(texto);
    }

    private static void Renglon(ColumnDescriptor columna, string etiqueta, decimal importe, bool destacado = false)
        => columna.Item().PaddingTop(4).Row(fila =>
        {
            var titulo = fila.RelativeItem().Text(etiqueta);
            if (destacado) titulo.Bold();
            var cifra = fila.ConstantItem(85).AlignRight().Text(Dinero(importe));
            if (destacado) cifra.Bold();
        });

    private static string Dinero(decimal importe) => importe.ToString("N2", Cultura);
}
