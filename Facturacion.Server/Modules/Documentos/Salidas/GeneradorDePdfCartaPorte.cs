using System.Globalization;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Data.Entidades.Transporte;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>Representación impresa de un CFDI de traslado con Carta Porte 3.1.</summary>
public sealed class GeneradorDePdfCartaPorte
{
    public byte[] Generar(Comprobante comprobante, TrasladoCartaPorte traslado, TimeZoneInfo zona, bool esBorrador = true)
        => Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(PageSizes.Letter);
            pagina.Margin(1.2f, Unit.Centimetre);
            pagina.DefaultTextStyle(x => x.FontFamily(Fonts.Calibri).FontSize(8));
            pagina.Header().Column(c =>
            {
                if (esBorrador)
                    c.Item().Border(1).Padding(5).AlignCenter().Text("BORRADOR — SIN VALIDEZ FISCAL").Bold();
                c.Item().PaddingTop(6).Row(fila =>
                {
                    fila.RelativeItem().Column(x => { x.Item().Text(comprobante.EmisorNombre).Bold().FontSize(12); x.Item().Text($"RFC {comprobante.EmisorRfc} · Régimen {comprobante.EmisorRegimenFiscal}"); });
                    fila.ConstantItem(190).AlignRight().Column(x =>
                    {
                        x.Item().Text(esBorrador ? "CARTA PORTE 3.1" : "CFDI DE TRASLADO").Bold().FontSize(12);
                        x.Item().Text($"IdCCP: {traslado.IdCcp ?? "Pendiente"}");
                        x.Item().Text(esBorrador ? "Sin timbrar" : $"UUID: {comprobante.Uuid?.ToString().ToUpperInvariant() ?? "—"}");
                    });
                });
            });
            pagina.Content().PaddingTop(10).Column(c =>
            {
                c.Item().Text("Datos del traslado").Bold().FontSize(10);
                c.Item().Text($"Salida: {Fecha(traslado.FechaSalidaUtc, zona)}   ·   Llegada: {Fecha(traslado.FechaLlegadaUtc, zona)}   ·   Distancia: {traslado.DistanciaRecorridaKm:N2} km");
                c.Item().PaddingTop(8).Text("Origen y destino").Bold().FontSize(10);
                foreach (var ubicacion in traslado.Ubicaciones.OrderBy(x => x.Orden))
                    c.Item().PaddingTop(3).Border(0.5f).Padding(4).Text($"{ubicacion.Tipo}: {ubicacion.Calle} {ubicacion.NumeroExterior} {ubicacion.NumeroInterior}, {ubicacion.Municipio}, {ubicacion.Estado}, C.P. {ubicacion.CodigoPostal}");
                c.Item().PaddingTop(8).Text("Autotransporte y operador").Bold().FontSize(10);
                c.Item().Text($"Vehículo: {traslado.VehiculoPlaca} · Configuración {traslado.VehiculoConfiguracionAutotransporte} · Permiso {traslado.VehiculoTipoPermiso} {traslado.VehiculoNumeroPermiso}");
                c.Item().Text($"Seguro: {traslado.VehiculoAseguradora} · Póliza {traslado.VehiculoPoliza}");
                c.Item().Text($"Operador: {traslado.FiguraNombre} · RFC {traslado.FiguraRfc} · Licencia {traslado.FiguraNumeroLicencia ?? "—"}");
                c.Item().PaddingTop(8).Text("Mercancías").Bold().FontSize(10);
                c.Item().Table(tabla =>
                {
                    tabla.ColumnsDefinition(x => { x.ConstantColumn(70); x.RelativeColumn(); x.ConstantColumn(60); x.ConstantColumn(60); x.ConstantColumn(65); });
                    tabla.Header(h => { foreach (var texto in new[] { "CLAVE SAT", "DESCRIPCIÓN", "CANTIDAD", "UNIDAD", "PESO KG" }) h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(texto).Bold().FontSize(7); });
                    foreach (var m in traslado.Mercancias.OrderBy(x => x.Orden)) { tabla.Cell().Padding(3).Text(m.ClaveProdServ); tabla.Cell().Padding(3).Text(m.Descripcion); tabla.Cell().Padding(3).AlignRight().Text(m.Cantidad.ToString("N6", CultureInfo.InvariantCulture)); tabla.Cell().Padding(3).Text(m.ClaveUnidad); tabla.Cell().Padding(3).AlignRight().Text(m.PesoEnKg.ToString("N3", CultureInfo.InvariantCulture)); }
                });
                c.Item().PaddingTop(5).AlignRight().Text($"Peso bruto total: {traslado.PesoBrutoTotalKg:N3} kg").Bold();
            });
            pagina.Footer().PaddingTop(6).BorderTop(0.5f).PaddingTop(4).Element(c => Pie(c, comprobante, esBorrador));
        })).GeneratePdf();

    private static string Fecha(DateTime utc, TimeZoneInfo zona) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zona).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    private static void Pie(IContainer contenedor, Comprobante comprobante, bool esBorrador)
    {
        if (esBorrador)
        {
            contenedor.AlignCenter().Text("Vista de revisión. No contiene UUID, folio, timbre ni certificación del SAT.").FontSize(7);
            return;
        }

        var qr = Qr(comprobante);
        contenedor.Row(fila =>
        {
            if (qr is not null)
                fila.ConstantItem(72).Height(72).Image(qr).FitArea();

            fila.RelativeItem().PaddingLeft(6).Column(c =>
            {
                c.Item().Text("Sello digital del CFDI").FontSize(6).Light();
                c.Item().Text(comprobante.SelloCfd ?? "—").FontFamily(Fonts.Consolas).FontSize(5);
                c.Item().PaddingTop(2).Text("Sello del SAT").FontSize(6).Light();
                c.Item().Text(comprobante.SelloSat ?? "—").FontFamily(Fonts.Consolas).FontSize(5);
                c.Item().PaddingTop(2).Text("Cadena original del complemento de certificación").FontSize(6).Light();
                c.Item().Text(comprobante.CadenaOriginalSat ?? "—").FontFamily(Fonts.Consolas).FontSize(5);
            });
        });
    }

    private static byte[]? Qr(Comprobante comprobante)
    {
        var expresion = ExpresionImpresa.Construir(
            comprobante.Uuid, comprobante.EmisorRfc, comprobante.ReceptorRfc,
            ExpresionImpresa.Total(comprobante.Total, 0), comprobante.SelloCfd);
        if (expresion is null) return null;

        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(expresion, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(datos).GetGraphic(20);
    }
}
