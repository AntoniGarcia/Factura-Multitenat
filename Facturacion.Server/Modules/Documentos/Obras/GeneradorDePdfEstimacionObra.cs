using System.Globalization;
using Facturacion.Server.Infra.Pdf;
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
                pagina.Margin(0.9f, Unit.Centimetre);
                pagina.DefaultTextStyle(texto => texto.FontFamily(Fonts.Calibri).FontSize(9));

                pagina.Header().Column(encabezado =>
                {
                    encabezado.Item().AlignRight().Text(factura.EmisorNombre).Bold().FontSize(15).FontColor(EstiloDeComprobante.Tinta);
                    encabezado.Item().PaddingTop(4).AlignRight().Text($"RFC {factura.EmisorRfc} · Régimen fiscal {factura.EmisorRegimenFiscal}");
                    encabezado.Item().PaddingVertical(7).Element(e => EstiloDeComprobante.Seccion(e, "ESTIMACIÓN DE OBRA"));
                    encabezado.Item().Text(obra.TipoObra switch
                    {
                        TiposDeObra.Publica => "Obra pública",
                        TiposDeObra.Privada => "Obra privada",
                        _ => "Tipo de obra sin clasificar"
                    }).FontSize(9);
                    encabezado.Item().PaddingTop(3).Text(Estado(factura))
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
                            if (obra.Amortizacion != 0)
                                Renglon(desglose, $"Amortización del anticipo ({obra.PorcentajeAmortizacion:N2} %)", -obra.Amortizacion);
                            Renglon(desglose, "Subtotal de la estimación", obra.SubtotalEstimacion, true);
                            Renglon(desglose, "IVA de los conceptos", obra.IvaEstimado);
                            Renglon(desglose, "Total de la estimación", obra.TotalEstimacion, true);
                            // Lo que sigue no está en el CFDI: se descuenta del pago. Con 0 % no aplica.
                            if (obra.Retenciones != 0)
                                Renglon(desglose, $"Retenciones ({obra.PorcentajeRetenciones ?? 0:N2} %)", -obra.Retenciones);
                            if (obra.Devoluciones != 0)
                                Renglon(desglose, $"Devoluciones ({obra.PorcentajeDevoluciones ?? 0:N2} %)", -obra.Devoluciones);
                            foreach (var deduccion in obra.Deducciones.Where(x => x.Porcentaje != 0))
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

            });
        }).GeneratePdf();

    private static string Estado(Comprobante factura) => factura.Estatus switch
    {
        "timbrado" or "en_cancelacion" => $"Factura {factura.Serie}{factura.Folio} · UUID {factura.Uuid}",
        "cancelado" => $"Factura {factura.Serie}{factura.Folio} · CANCELADA",
        _ => "BORRADOR"
    };

    private static void Cabecera(TableCellDescriptor celda, string texto)
        => EstiloDeComprobante.Cabecera(celda.Cell()).Text(texto).FontSize(8);

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
