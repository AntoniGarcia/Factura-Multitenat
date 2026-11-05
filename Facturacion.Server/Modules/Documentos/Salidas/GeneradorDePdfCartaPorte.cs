using System.Globalization;
using Facturacion.Server.Infra.Pdf;
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
    /// <param name="leyendaDelTimbre">El atributo <c>Leyenda</c> del timbre; ver <see cref="DatosDelPdf"/>.</param>
    public byte[] Generar(Comprobante comprobante, TrasladoCartaPorte traslado, TimeZoneInfo zona, bool esBorrador = true,
        string? leyendaDelTimbre = null)
        => Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(PageSizes.Letter);
            pagina.Margin(0.9f, Unit.Centimetre);
            pagina.DefaultTextStyle(x => x.FontFamily(Fonts.Calibri).FontSize(8));
            var datos = new DatosDelPdf(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(comprobante.FechaEmisionUtc, DateTimeKind.Utc), zona),
                2, null, esBorrador, LeyendaDelTimbre: leyendaDelTimbre) { ZonaHoraria = zona };
            pagina.Header().Element(e => GeneradorDePdfCfdi.Encabezado(e, comprobante, datos));
            pagina.Content().PaddingTop(4).Column(c =>
            {
                c.Item().Element(e => GeneradorDePdfCfdi.Receptor(e, comprobante, datos));
                c.Item().PaddingVertical(6).Text($"Carta Porte 3.1 · IdCCP: {traslado.IdCcp ?? "Pendiente"}").Bold();
                c.Item().Element(e => EstiloDeComprobante.Seccion(e, "Datos del traslado"));
                c.Item().Text($"Salida: {Fecha(traslado.FechaSalidaUtc, zona)}   ·   Llegada: {Fecha(traslado.FechaLlegadaUtc, zona)}   ·   Distancia: {traslado.DistanciaRecorridaKm:N2} km");
                c.Item().PaddingTop(8).Element(e => EstiloDeComprobante.Seccion(e, "Origen y destino"));
                foreach (var ubicacion in traslado.Ubicaciones.OrderBy(x => x.Orden))
                    c.Item().PaddingTop(3).Border(0.5f).Padding(4).Text($"{ubicacion.Tipo}: {ubicacion.NombreRemitenteDestinatario ?? ubicacion.RfcRemitenteDestinatario} · RFC {ubicacion.RfcRemitenteDestinatario} · {ubicacion.Calle} {ubicacion.NumeroExterior} {ubicacion.NumeroInterior}, {ubicacion.Municipio}, {ubicacion.Estado}, C.P. {ubicacion.CodigoPostal}");
                c.Item().PaddingTop(8).Element(e => EstiloDeComprobante.Seccion(e, "Autotransporte y operador"));
                c.Item().Text($"Vehículo: {traslado.VehiculoPlaca} · Configuración {traslado.VehiculoConfiguracionAutotransporte} · Permiso {traslado.VehiculoTipoPermiso} {traslado.VehiculoNumeroPermiso}");
                foreach (var remolque in traslado.Remolques.OrderBy(x => x.Orden))
                    c.Item().Text($"Remolque {remolque.Orden}: {remolque.SubTipoRem} · Placa {remolque.Placa}");
                c.Item().Text($"Seguro: {traslado.VehiculoAseguradora} · Póliza {traslado.VehiculoPoliza}");
                if (traslado.AseguraMedAmbiente is { } aseguradoraAmbiental)
                    c.Item().Text($"Seguro ambiental: {aseguradoraAmbiental} · Póliza {traslado.PolizaMedAmbiente}");
                c.Item().Text($"Operador: {traslado.FiguraNombre} · RFC {traslado.FiguraRfc} · Licencia {traslado.FiguraNumeroLicencia ?? "—"}");
                c.Item().PaddingTop(8).Element(e => EstiloDeComprobante.Seccion(e, "Mercancías"));
                c.Item().Table(tabla =>
                {
                    tabla.ColumnsDefinition(x => { x.ConstantColumn(62); x.RelativeColumn(); x.ConstantColumn(55); x.ConstantColumn(57); x.ConstantColumn(60); x.ConstantColumn(64); });
                    tabla.Header(h => { foreach (var texto in new[] { "CLAVE SAT", "DESCRIPCIÓN", "CANTIDAD", "UNIDAD", "PESO UNIT. KG", "PESO TOTAL KG" }) EstiloDeComprobante.Cabecera(h.Cell()).Text(texto); });
                    foreach (var m in traslado.Mercancias.OrderBy(x => x.Orden))
                    {
                        EstiloDeComprobante.Celda(tabla.Cell()).Text(m.ClaveProdServ);
                        var descripcion = string.IsNullOrWhiteSpace(m.Dimensiones) ? m.Descripcion : $"{m.Descripcion} · {m.Dimensiones}";
                        if (m.MaterialPeligroso is { } material) descripcion += $" · Material peligroso: {material}";
                        if (m.CveMaterialPeligroso is { } claveMaterial) descripcion += $" · Clave {claveMaterial} · Embalaje {m.Embalaje} {m.DescripEmbalaje}";
                        EstiloDeComprobante.Celda(tabla.Cell()).Text(descripcion);
                        EstiloDeComprobante.Celda(tabla.Cell()).AlignRight().Text(m.Cantidad.ToString("N6", CultureInfo.InvariantCulture));
                        EstiloDeComprobante.Celda(tabla.Cell()).Text(string.IsNullOrWhiteSpace(m.Unidad) ? m.ClaveUnidad : $"{m.ClaveUnidad} · {m.Unidad}");
                        EstiloDeComprobante.Celda(tabla.Cell()).AlignRight().Text(m.PesoUnitarioKg?.ToString("N6", CultureInfo.InvariantCulture) ?? "—");
                        EstiloDeComprobante.Celda(tabla.Cell()).AlignRight().Text(m.PesoEnKg.ToString("N3", CultureInfo.InvariantCulture));
                    }
                });
                c.Item().PaddingTop(5).AlignRight().Text($"Peso bruto total: {traslado.PesoBrutoTotalKg:N3} kg").Bold();
                c.Item().AlignRight().Text($"Renglones de mercancía: {traslado.Mercancias.Count}");
                foreach (var grupo in traslado.Mercancias.GroupBy(x => x.ClaveUnidad).OrderBy(x => x.Key))
                    c.Item().AlignRight().Text($"Cantidad total {grupo.Key}: {grupo.Sum(x => x.Cantidad).ToString("0.######", CultureInfo.InvariantCulture)}");

                if (comprobante.Relacionados.Count > 0)
                {
                    c.Item().PaddingTop(8).Element(e => EstiloDeComprobante.Seccion(e, "CFDI relacionados"));
                    foreach (var relacionado in comprobante.Relacionados.OrderBy(x => x.TipoRelacion).ThenBy(x => x.UuidRelacionado))
                        c.Item().Text($"Tipo {relacionado.TipoRelacion} · UUID {relacionado.UuidRelacionado:D}").FontSize(7);
                }

                if (!string.IsNullOrWhiteSpace(comprobante.Observaciones))
                {
                    c.Item().PaddingTop(8).Element(e => EstiloDeComprobante.Seccion(e, "Observaciones internas"));
                    c.Item().Text(comprobante.Observaciones);
                }
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
