using System.Globalization;
using Facturacion.Server.Infra.Pdf;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Impuestos;
using Facturacion.Shared.ComercioExterior;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;


namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>Lo que el PDF necesita y no está dentro del comprobante.</summary>
/// <param name="Logo">Logo de la empresa, ya descifrado del almacén. Nulo si no tiene.</param>
/// <param name="LeyendaDelTimbre">
/// El atributo <c>Leyenda</c> del timbre fiscal: lo que el SAT o el PAC quieren que lea quien
/// recibe el comprobante. Con el PAC simulado dice que no tiene validez fiscal.
/// </param>
public sealed record DatosDelPdf(
    DateTime FechaLocal,
    int Decimales,
    byte[]? Logo,
    bool EsBorrador = false,
    DatosNotarialesDelPdf? Notaria = null,
    DatosComercioExteriorDto? ComercioExterior = null,
    IReadOnlyList<ImpuestoLocalCfdi>? ImpuestosLocales = null,
    string? LeyendaDelTimbre = null)
{
    public TimeZoneInfo? ZonaHoraria { get; init; }
}

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
///. Reimprimir una factura de hace dos años tiene que dar el mismo papel.
/// </summary>
public sealed class GeneradorDePdfCfdi
{
    private static readonly string[] EncabezadosConceptos =
        ["CANTIDAD", "CLAVE UNIDAD", "CLAVE DEL PRODUCTO", "DESCRIPCIÓN", "OBJ. IMP.", "P. UNITARIO", "IMPORTE"];

    public byte[] Generar(Comprobante comprobante, DatosDelPdf datos)
        => Construir(comprobante, datos).GeneratePdf();

    public IDocument Construir(Comprobante comprobante, DatosDelPdf datos)
    {
        // El QR lleva el total tal como está en el XML. En un pago la raíz dice Total="0" con
        // moneda XXX; los decimales de la moneda del pago lo volverían «0.00» y el SAT no
        // encontraría el comprobante.
        var qr = Qr(comprobante, comprobante.TipoDeComprobante == "P" ? 0 : datos.Decimales);

        return Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.Letter);
                pagina.Margin(0.9f, Unit.Centimetre);
                pagina.DefaultTextStyle(t => t.FontSize(8).FontFamily(Fonts.Calibri).FontColor(EstiloDeComprobante.Texto));

                pagina.Header().Element(e => Encabezado(e, comprobante, datos));
                pagina.Content().Element(e => Cuerpo(e, comprobante, datos));
                pagina.Footer().Column(pie =>
                {
                    pie.Item().Element(e => Pie(e, comprobante, qr, datos.EsBorrador));
                    pie.Item().PaddingTop(3).AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(6).FontColor(EstiloDeComprobante.Secundario));
                        t.Span("Página "); t.CurrentPageNumber(); t.Span(" de "); t.TotalPages();
                    });
                });
            });
        });
    }

    // ── Encabezado: emisor, logo y los identificadores fiscales ─────────────────────────

    internal static void Encabezado(IContainer contenedor, Comprobante c, DatosDelPdf datos)
        => contenedor.PaddingBottom(5).Column(columna =>
        {
            if (datos.EsBorrador)
                columna.Item().PaddingBottom(5).Element(e => EstiloDeComprobante.Seccion(e, "BORRADOR - SIN VALIDEZ FISCAL"));
            else if (!string.IsNullOrWhiteSpace(datos.LeyendaDelTimbre))
                columna.Item().PaddingBottom(5).Text(datos.LeyendaDelTimbre).FontSize(7).Bold();
            if (!datos.EsBorrador && c.Estatus is "cancelado" or "en_cancelacion")
                columna.Item().PaddingBottom(5).Element(e => EstiloDeComprobante.Seccion(e,
                    c.Estatus == "cancelado" ? "CFDI CANCELADO" : "CFDI EN PROCESO DE CANCELACIÓN"));

            columna.Item().Row(fila =>
            {
                if (datos.Logo is { Length: > 0 } logo)
                    fila.ConstantItem(105).Height(73).PaddingRight(12).Image(logo).FitArea();
                fila.RelativeItem().Column(emisor =>
                {
                    emisor.Item().AlignRight().Text(c.EmisorNombre).Bold().FontSize(15).FontColor(EstiloDeComprobante.Tinta);
                    emisor.Item().PaddingTop(8).AlignRight().Text($"Lugar de expedición: C.P. {c.LugarExpedicion}")
                        .FontColor(EstiloDeComprobante.Tinta);
                    emisor.Item().PaddingTop(6).AlignRight().Text($"Régimen fiscal: {c.EmisorRegimenFiscal}");
                    emisor.Item().PaddingTop(3).AlignRight().Text($"R.F.C. {c.EmisorRfc}").Bold().FontSize(10);
                });
            });
            columna.Item().PaddingVertical(6).AlignCenter().Text(TituloDelTipo(c.TipoDeComprobante)).Bold().FontSize(12);
            columna.Item().Table(tabla =>
            {
                tabla.ColumnsDefinition(x => { x.RelativeColumn(0.45f); x.RelativeColumn(0.8f); x.RelativeColumn(2.3f); x.RelativeColumn(1.6f); x.RelativeColumn(1.6f); });
                foreach (var titulo in new[] { "Versión", "Folio interno", "Folio fiscal", "No. de serie del CSD SAT", "No. de serie del CSD emisor" })
                    EstiloDeComprobante.Cabecera(tabla.Cell()).Text(titulo);
                foreach (var valor in new[] { "4.0", datos.EsBorrador ? "Sin asignar" : FolioInterno(c),
                    datos.EsBorrador ? "No timbrado" : c.Uuid?.ToString().ToUpperInvariant() ?? "-",
                    datos.EsBorrador ? "No timbrado" : c.NoCertificadoSat ?? "-",
                    datos.EsBorrador ? "No timbrado" : c.NoCertificadoEmisor ?? "-" })
                    EstiloDeComprobante.Celda(tabla.Cell()).Text(valor).FontSize(7);
            });
        });

    internal static void Receptor(IContainer contenedor, Comprobante c, DatosDelPdf datos)
        => contenedor.Row(fila =>
        {
            fila.RelativeItem().PaddingRight(8).Column(cliente =>
            {
                cliente.Item().Row(r =>
                {
                    r.ConstantItem(62).Element(e => EstiloDeComprobante.Seccion(e, "Cliente"));
                    r.RelativeItem().Padding(4).Text(c.ReceptorNombre).Bold();
                });
                cliente.Item().PaddingLeft(66).PaddingTop(3).Text($"Domicilio fiscal: C.P. {c.ReceptorDomicilioFiscal}");
                cliente.Item().PaddingTop(12).Text(t => { t.Span("Uso del CFDI: ").Bold(); t.Span(c.ReceptorUsoCfdi); });
                cliente.Item().PaddingTop(3).Text(t => { t.Span("Régimen fiscal: ").Bold(); t.Span(c.ReceptorRegimenFiscal); });
                if (!string.IsNullOrWhiteSpace(c.ReceptorNumRegIdTrib))
                    cliente.Item().PaddingTop(3).Text($"Registro tributario: {c.ReceptorNumRegIdTrib} · {c.ReceptorResidenciaFiscal}");
            });
            fila.ConstantItem(160).Column(detalle =>
            {
                detalle.Item().Row(r =>
                {
                    r.ConstantItem(42).Element(e => EstiloDeComprobante.Seccion(e, "R.F.C."));
                    r.RelativeItem().Padding(4).Text(c.ReceptorRfc);
                });
                detalle.Item().Element(e => EstiloDeComprobante.Seccion(e, "Fecha y hora de certificación"));
                var certificacion = !datos.EsBorrador && c.FechaTimbradoUtc is { } fecha
                    ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fecha, DateTimeKind.Utc), datos.ZonaHoraria ?? TimeZoneInfo.Utc)
                        .ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture) : "No timbrado";
                detalle.Item().Padding(4).Text(certificacion);
                detalle.Item().Element(e => EstiloDeComprobante.Seccion(e, "Fecha y hora de emisión"));
                detalle.Item().Padding(4).Text(datos.FechaLocal.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture));
            });
        });

    private static void Cuerpo(IContainer contenedor, Comprobante c, DatosDelPdf datos)
        => contenedor.Column(columna =>
        {
            columna.Item().Element(e => Receptor(e, c, datos));
            columna.Item().PaddingTop(5).Row(fila =>
            {
                fila.ConstantItem(100).Element(e => EstiloDeComprobante.Seccion(e, "Lugar de expedición"));
                fila.RelativeItem().Padding(4).Text(c.LugarExpedicion);
            });
            if (c.TipoDeComprobante == "P")
                columna.Item().PaddingTop(5).Element(e => ComplementoDePago(e, c.Pagos, datos.Decimales,
                    datos.ZonaHoraria ?? throw new InvalidOperationException("Falta el huso horario del PDF de pagos.")));
            else
            {
                columna.Item().Element(e => Conceptos(e, c, datos.Decimales));
                columna.Item().PaddingTop(8).Element(e => Totales(e, c, datos.Decimales, datos.ImpuestosLocales ?? []));
                if (datos.Notaria is { } notaria)
                    columna.Item().PaddingTop(10).Element(e => SeccionNotarial(e, notaria, datos.Decimales));
            }
            if (datos.ComercioExterior is { } comercio)
                columna.Item().PaddingTop(10).Element(e => SeccionComercioExterior(e, c, comercio));
            if (!string.IsNullOrWhiteSpace(c.Observaciones))
            {
                columna.Item().PaddingTop(8).Element(e => EstiloDeComprobante.Seccion(e, "Observaciones"));
                columna.Item().Padding(4).Text(c.Observaciones);
            }
        });

    private static void SeccionComercioExterior(
        IContainer contenedor, Comprobante comprobante, DatosComercioExteriorDto datos)
        => contenedor.PreventPageBreak().Column(columna =>
        {
            columna.Item().PaddingBottom(4).Element(e => EstiloDeComprobante.Seccion(e, "COMERCIO EXTERIOR 2.0"));
            columna.Item().Border(0.5f).Padding(6).Column(detalle =>
            {
                detalle.Item().Text($"Pedimento {datos.ClavePedimento}  ·  INCOTERM {datos.Incoterm ?? "—"}").Bold();
                detalle.Item().Text($"Tipo de cambio USD {Cifra(datos.TipoCambioUsd!.Value, 6)}  ·  Total USD {Cifra(datos.TotalUsd!.Value, 2)}");
                detalle.Item().Text($"Certificado de origen: {(datos.CertificadoOrigen ? $"Sí · {datos.NumeroCertificadoOrigen}" : "No")}");
                if (!string.IsNullOrWhiteSpace(datos.NumeroExportadorConfiable))
                    detalle.Item().Text($"Exportador confiable: {datos.NumeroExportadorConfiable}");
                if (!string.IsNullOrWhiteSpace(datos.NumeroRegistroTributario))
                    detalle.Item().Text($"Registro tributario del receptor: {datos.NumeroRegistroTributario}  ·  Residencia fiscal {datos.ResidenciaFiscal ?? "—"}");
                if (!string.IsNullOrWhiteSpace(datos.CurpEmisor))
                    detalle.Item().Text($"CURP del emisor: {datos.CurpEmisor}");
                if (!string.IsNullOrWhiteSpace(datos.Observaciones))
                    detalle.Item().Text($"Observaciones: {datos.Observaciones}");
            });

            columna.Item().PaddingTop(5).Text("Domicilios del complemento").Bold();
            columna.Item().Text($"Emisor: {DireccionComercio(datos.DomicilioEmisor)}").FontSize(7);
            columna.Item().Text($"Receptor: {DireccionComercio(datos.DomicilioReceptor)}").FontSize(7);

            columna.Item().PaddingTop(6).Text("Mercancías exportadas").Bold();
            foreach (var mercancia in (datos.Mercancias ?? []).OrderBy(x => x.OrdenConcepto))
            {
                var concepto = comprobante.Conceptos.First(x => x.Orden == mercancia.OrdenConcepto);
                columna.Item().PaddingTop(4).BorderBottom(0.5f).PaddingBottom(3).Column(detalle =>
                {
                    detalle.Item().Text($"{mercancia.OrdenConcepto}. {concepto.NoIdentificacion} · {concepto.Descripcion}").Bold();
                    detalle.Item().Text(
                        $"Fracción {mercancia.FraccionArancelaria ?? "—"}  ·  Unidad aduanera {mercancia.UnidadAduana ?? "—"}  ·  Cantidad {NumeroOpcional(mercancia.CantidadAduana, 3)}");
                    detalle.Item().Text(
                        $"Valor unitario USD {NumeroOpcional(mercancia.ValorUnitarioAduana, 6)}  ·  Valor USD {NumeroOpcional(mercancia.ValorDolares, 4)}");
                    if (new[] { mercancia.Marca, mercancia.Modelo, mercancia.Submodelo, mercancia.NumeroSerie }
                        .Any(x => !string.IsNullOrWhiteSpace(x)))
                        detalle.Item().Text($"Marca {mercancia.Marca ?? "—"}  ·  Modelo {mercancia.Modelo ?? "—"}  ·  Submodelo {mercancia.Submodelo ?? "—"}  ·  Serie {mercancia.NumeroSerie ?? "—"}")
                            .FontSize(7);
                });
            }
        });

    private static string DireccionComercio(DomicilioComercioExteriorDto domicilio)
        => string.Join(", ", new[]
        {
            domicilio.Calle, domicilio.NumeroExterior, domicilio.NumeroInterior,
            domicilio.Colonia, domicilio.Localidad, domicilio.Referencia,
            domicilio.Municipio, domicilio.Estado, domicilio.Pais, domicilio.CodigoPostal
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string NumeroOpcional(decimal? valor, int decimales)
        => valor is null ? "—" : Cifra(valor.Value, decimales);

    private static void SeccionNotarial(IContainer contenedor, DatosNotarialesDelPdf datos, int decimales)
        => contenedor.PreventPageBreak().Column(columna =>
        {
            columna.Item().PaddingBottom(4).Element(e => EstiloDeComprobante.Seccion(e, "DATOS NOTARIALES"));
            columna.Item().Border(0.5f).Padding(6).Column(detalle =>
            {
                detalle.Item().Text($"Instrumento notarial {datos.NumeroInstrumento}  ·  Fecha {datos.FechaInstrumento:dd/MM/yyyy}").Bold();
                detalle.Item().Text($"Notaría {datos.NumeroNotaria}  ·  Entidad {datos.EstadoNotaria}  ·  CURP {datos.CurpNotario}");
                if (!string.IsNullOrWhiteSpace(datos.Adscripcion))
                    detalle.Item().Text($"Adscripción: {datos.Adscripcion}");
                detalle.Item().PaddingTop(3).Text(
                    $"Operación {Cifra(datos.MontoOperacion, decimales)}  ·  Subtotal {Cifra(datos.Subtotal, decimales)}  ·  IVA {Cifra(datos.Iva, decimales)}");
            });

            columna.Item().PaddingTop(6).Text($"Inmuebles ({datos.Inmuebles.Count})").Bold();
            foreach (var inmueble in datos.Inmuebles)
                columna.Item().PaddingTop(2).Text($"Tipo {inmueble.Tipo}  ·  {inmueble.Direccion}");

            columna.Item().PaddingTop(6).Text("Enajenantes").Bold();
            if (datos.Enajenantes.Count == 0)
                columna.Item().Text("Sin capturar en este borrador.").FontSize(7).Light();
            foreach (var persona in datos.Enajenantes)
                columna.Item().PaddingTop(2).Element(e => ParteNotarial(e, persona));

            columna.Item().PaddingTop(6).Text("Adquirentes").Bold();
            if (datos.Adquirentes.Count == 0)
                columna.Item().Text("Sin capturar en este borrador.").FontSize(7).Light();
            foreach (var persona in datos.Adquirentes)
                columna.Item().PaddingTop(2).Element(e => ParteNotarial(e, persona));
        });

    private static void ParteNotarial(IContainer contenedor, ParteNotarialDelPdf persona)
        => contenedor.PreventPageBreak().Column(columna =>
        {
            columna.Item().Text(persona.NombreCompleto);
            columna.Item().Text($"RFC {persona.Rfc}" +
                (persona.Curp is null ? string.Empty : $"  ·  CURP {persona.Curp}") +
                (persona.Porcentaje is null ? string.Empty :
                    $"  ·  Participación {persona.Porcentaje.Value.ToString("F2", CultureInfo.InvariantCulture)} %"))
                .FontSize(7).Light();
        });

    private static void Conceptos(IContainer contenedor, Comprobante c, int decimales)
        => contenedor.Border(0.4f).BorderColor(EstiloDeComprobante.Borde).Table(tabla =>
        {
            tabla.ColumnsDefinition(ColumnasDeConceptos);
            tabla.Header(h =>
            {
                foreach (var titulo in EncabezadosConceptos)
                    EstiloDeComprobante.Cabecera(h.Cell()).Text(titulo).FontSize(6.5f);
            });
            foreach (var concepto in c.Conceptos.OrderBy(x => x.Orden))
            {
                tabla.Cell().ColumnSpan(7).PreventPageBreak().Table(renglon =>
                {
                    renglon.ColumnsDefinition(ColumnasDeConceptos);
                EstiloDeComprobante.Celda(renglon.Cell()).AlignRight().Text(concepto.Cantidad.ToString("#,0.00####", CultureInfo.InvariantCulture));
                EstiloDeComprobante.Celda(renglon.Cell()).Text(concepto.ClaveUnidad);
                EstiloDeComprobante.Celda(renglon.Cell()).Text(concepto.ClaveProdServ);
                EstiloDeComprobante.Celda(renglon.Cell()).Text(concepto.Descripcion);
                EstiloDeComprobante.Celda(renglon.Cell()).AlignCenter().Text(concepto.ObjetoImp);
                EstiloDeComprobante.Celda(renglon.Cell()).AlignRight().Text(concepto.ValorUnitario.ToString("#,0.00####", CultureInfo.InvariantCulture));
                EstiloDeComprobante.Celda(renglon.Cell()).AlignRight().Text(Cifra(concepto.Importe, decimales));
                if (concepto.Impuestos.Count == 0) return;
                EstiloDeComprobante.Celda(renglon.Cell().ColumnSpan(3));
                EstiloDeComprobante.Celda(renglon.Cell().ColumnSpan(4)).Table(sub =>
                {
                    sub.ColumnsDefinition(x => { x.RelativeColumn(); x.RelativeColumn(); x.RelativeColumn(); x.RelativeColumn(); x.RelativeColumn(); });
                    foreach (var titulo in new[] { "BASE", "IMPUESTO", "TIPO FACTOR", "TASA O CUOTA", "IMPORTE" })
                        sub.Cell().Padding(2).Text(titulo).Bold().FontSize(6);
                    foreach (var impuesto in concepto.Impuestos)
                    {
                        sub.Cell().Padding(2).Text(Cifra(impuesto.Base, 6)).FontSize(6.5f);
                        sub.Cell().Padding(2).Text(NombreDeImpuesto(impuesto.Impuesto, impuesto.EsRetencion)).FontSize(6.5f);
                        sub.Cell().Padding(2).Text(impuesto.TipoFactor).FontSize(6.5f);
                        sub.Cell().Padding(2).Text(impuesto.TasaOCuota is { } t ? Cifra(t, 6) : "-").FontSize(6.5f);
                        sub.Cell().Padding(2).AlignRight().Text(impuesto.Importe is { } i ? Cifra(i, 6) : "-").FontSize(6.5f);
                    }
                });
                });
            }
        });

    private static void ColumnasDeConceptos(TableColumnsDefinitionDescriptor columnas)
    {
        columnas.ConstantColumn(43); columnas.ConstantColumn(44); columnas.ConstantColumn(55); columnas.RelativeColumn();
        columnas.ConstantColumn(30); columnas.ConstantColumn(64); columnas.ConstantColumn(67);
    }

    private static void Totales(IContainer contenedor, Comprobante c, int decimales, IReadOnlyList<ImpuestoLocalCfdi> locales)
        => contenedor.PreventPageBreak().Row(fila =>
        {
            fila.RelativeItem().PaddingRight(18).Column(pago =>
            {
                pago.Item().Text(t => { t.Span("Método de pago: ").Bold(); t.Span(c.MetodoPago ?? "-"); });
                pago.Item().PaddingTop(3).Text(t => { t.Span("Forma de pago: ").Bold(); t.Span(c.FormaPago ?? "-"); });
                pago.Item().PaddingTop(8).Text($"Moneda: {c.Moneda}    T.C.: {(c.TipoCambio is { } tc ? Cifra(tc, 6) : c.Moneda == "MXN" ? "1" : "-")}");
                if (!string.IsNullOrWhiteSpace(c.CondicionesDePago))
                    pago.Item().PaddingTop(5).Text($"Condiciones de pago: {c.CondicionesDePago}");
            });
            fila.ConstantItem(215).Column(totales =>
            {
                totales.Item().Element(e => Dato(e, "SUBTOTAL", Cifra(c.SubTotal, decimales)));
                totales.Item().PaddingTop(3).Element(e => Dato(e, "DESCUENTO", Cifra(c.Descuento, decimales)));
                foreach (var retencion in new[] { false, true })
                {
                    var total = retencion ? c.TotalImpuestosRetenidos : c.TotalImpuestosTrasladados;
                    if (total <= 0) continue;
                    var grupos = c.Conceptos.SelectMany(x => x.Impuestos)
                        .Where(x => x.EsRetencion == retencion && x.Importe is not null)
                        .GroupBy(x => (x.Impuesto, x.TipoFactor, x.TasaOCuota)).ToArray();
                    if (grupos.Length > 0 && decimal.Round(grupos.Sum(g => g.Sum(x => x.Importe!.Value)), decimales) == decimal.Round(total, decimales))
                        foreach (var grupo in grupos)
                            totales.Item().PaddingTop(3).Element(e => Dato(e,
                                NombreDeImpuesto(grupo.Key.Impuesto, retencion) + (grupo.Key.TasaOCuota is { } tasa && grupo.Key.TipoFactor == "Tasa" ? $" {tasa * 100:0.##} %" : ""),
                                Cifra(grupo.Sum(x => x.Importe!.Value), decimales)));
                    else
                        totales.Item().PaddingTop(3).Element(e => Dato(e, retencion ? "Impuestos retenidos" : "Impuestos trasladados", Cifra(total, decimales)));
                }
                foreach (var local in locales)
                    totales.Item().PaddingTop(3).Element(e => Dato(e,
                        $"{local.Nombre} ({Cifra(local.Tasa, 2)} %){(local.EsRetencion ? " retenido" : "")} · impuesto local",
                        (local.EsRetencion ? "−" : "") + Cifra(local.Importe, 2)));
                totales.Item().PaddingTop(8).Background(EstiloDeComprobante.Azul).Padding(5)
                    .DefaultTextStyle(t => t.FontColor(EstiloDeComprobante.Blanco))
                    .Element(e => Dato(e, $"TOTAL {c.Moneda}", Cifra(c.Total, decimales), negrita: true, anchoDelValor: 95));
            });
        });

    // ── Pie: QR, sellos y cadena original ───────────────────────────────────────────────

    private static void Pie(IContainer contenedor, Comprobante c, byte[]? qr, bool esBorrador)
        => contenedor.PaddingTop(6).BorderTop(0.5f).PaddingTop(4).Element(elemento =>
        {
            if (esBorrador)
            {
                elemento.Column(columna =>
                {
                    columna.Item().Text("BORRADOR SIN VALIDEZ FISCAL").Bold().FontSize(8).AlignCenter();
                    columna.Item().PaddingTop(2).Text(
                        "Esta vista previa no es un CFDI. No contiene UUID, sellos, código QR fiscal ni certificación del SAT.")
                        .FontSize(7)
                        .AlignCenter();
                });
                return;
            }

            elemento.Row(fila =>
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

    private static void Dato(IContainer contenedor, string etiqueta, string valor, bool negrita = false,
        float anchoDelValor = 120)
        => contenedor.Row(fila =>
        {
            var titulo = fila.RelativeItem().Text(etiqueta).FontSize(negrita ? 10 : 8);
            if (negrita) titulo.Bold();

            var texto = fila.ConstantItem(anchoDelValor).AlignRight().Text(valor).FontSize(negrita ? 10 : 8);
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

    // ── Complemento de pago ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Más angosto que el de los totales: en los datos del pago caben tres pares por renglón,
    /// y con el ancho normal las etiquetas se parten en dos líneas.
    /// </summary>
    private const float AnchoDeValorDelPago = 90;

    private static void ComplementoDePago(IContainer contenedor, List<Pago> pagos, int decimales, TimeZoneInfo zona)
        => contenedor.Column(columna =>
        {
            foreach (var pago in pagos)
            {
                columna.Item().PaddingBottom(6).Border(0.5f).Padding(5).Column(bloque =>
                {
                    bloque.Item().Element(e => EstiloDeComprobante.Seccion(e, "DATOS DEL PAGO"));

                    // Tres pares etiqueta-valor por renglón: sin separación, el valor de una
                    // columna queda pegado a la etiqueta de la siguiente.
                    bloque.Item().PaddingTop(3).Row(fila =>
                    {
                        fila.Spacing(16);
                        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(pago.FechaPagoUtc, DateTimeKind.Utc), zona);
                        fila.RelativeItem().Element(e => Dato(e, "Fecha de pago", fechaLocal.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture), anchoDelValor: AnchoDeValorDelPago));
                        fila.RelativeItem().Element(e => Dato(e, "Forma de pago", pago.FormaDePagoP, anchoDelValor: AnchoDeValorDelPago));
                        fila.RelativeItem().Element(e => Dato(e, "Moneda", pago.MonedaP, anchoDelValor: AnchoDeValorDelPago));
                    });

                    bloque.Item().Row(fila =>
                    {
                        fila.Spacing(16);
                        fila.RelativeItem().Element(e => Dato(e, "Monto", Cifra(pago.Monto, decimales), anchoDelValor: AnchoDeValorDelPago));
                        fila.RelativeItem().Element(e => Dato(e, "Tipo de cambio", pago.TipoCambioP is { } tc ? Cifra(tc, 6) : "—", anchoDelValor: AnchoDeValorDelPago));
                        fila.RelativeItem().Element(e => Dato(e, "No. de operación", pago.NumOperacion ?? "—", anchoDelValor: AnchoDeValorDelPago));
                    });

                    // Los datos bancarios son opcionales en el complemento: solo se muestran si vienen capturados.
                    if (pago.CtaOrdenante is not null || pago.CtaBeneficiario is not null)
                    {
                        bloque.Item().PaddingTop(2).Row(fila =>
                        {
                            fila.Spacing(16);
                            fila.RelativeItem().Element(e => Dato(e, "Cuenta ordenante", pago.CtaOrdenante ?? "—", anchoDelValor: AnchoDeValorDelPago));
                            fila.RelativeItem().Element(e => Dato(e, "Banco ordenante", pago.RfcEmisorCtaOrd ?? pago.NomBancoOrdExt ?? "—", anchoDelValor: AnchoDeValorDelPago));
                            fila.RelativeItem().Element(e => Dato(e, "Cuenta beneficiaria", pago.CtaBeneficiario ?? "—", anchoDelValor: AnchoDeValorDelPago));
                        });
                    }

                    bloque.Item().PaddingTop(4).Element(e => DocumentosPagados(e, pago.Documentos, decimales));
                });
            }
            columna.Item().PaddingTop(4).Element(e => TotalesDelComplemento(e, pagos));
        });

    private static void TotalesDelComplemento(IContainer contenedor, List<Pago> pagos)
    => contenedor.Column(columna =>
    {
        const int decimales = 2;
        // Comparte los grupos y su precisión con el XML; los totales del complemento son MXN.
        var impuestos = pagos
            .SelectMany(p => GeneradorDeXmlPago.AgruparImpuestos(p).Select(i => i with
            {
                Base = i.Base * GeneradorDeXmlPago.CambioAMxn(p),
                Importe = i.Importe * GeneradorDeXmlPago.CambioAMxn(p)
            }))
            .ToArray();

        var retenciones = impuestos
            .Where(i => i.EsRetencion)
            .GroupBy(i => i.Impuesto)
            .Select(g => (Etiqueta: NombreDeImpuesto(g.Key, esRetencion: true), Importe: g.Sum(i => i.Importe ?? 0m)))
            .OrderBy(r => r.Etiqueta);

        var traslados = impuestos
            .Where(i => !i.EsRetencion)
            .GroupBy(i => (i.Impuesto, i.TipoFactor, i.TasaOCuota))
            .Select(g => (
                Etiqueta: EtiquetaDeTraslado(g.Key.Impuesto, g.Key.TipoFactor, g.Key.TasaOCuota),
                Base: g.Sum(i => i.Base),
                Importe: g.Sum(i => i.Importe ?? 0m)))
            .OrderBy(t => t.Etiqueta);

        columna.Item().PaddingTop(3).Element(e => EstiloDeComprobante.Seccion(e, "TOTALES DEL COMPLEMENTO (MXN)"));

        foreach (var (etiqueta, importe) in retenciones)
        {
            columna.Item().Element(e => Dato(e, $"Retención {etiqueta}", Cifra(importe, decimales)));
        }

        foreach (var (etiqueta, baseImportes, importe) in traslados)
        {
            if (importe <= 0 && baseImportes <= 0) continue;
            columna.Item().Element(e => Dato(e, $"Traslado {etiqueta} (base {Cifra(baseImportes, decimales)})", Cifra(importe, decimales)));
        }

        columna.Item().PaddingTop(2).BorderTop(0.5f).Element(e =>
            Dato(e, "Monto total de pagos (MXN)", Cifra(pagos.Sum(p => p.Monto * GeneradorDeXmlPago.CambioAMxn(p)), decimales), negrita: true));
    });

    private static string EtiquetaDeTraslado(string impuesto, string tipoFactor, decimal? tasaOCuota)
    {
        var nombre = NombreDeImpuesto(impuesto, esRetencion: false);

        if (tipoFactor == TiposDeFactor.Exento) return $"{nombre} exento";

        return tasaOCuota is { } t ? $"{nombre} {t:P2}" : nombre;
    }

    private static readonly string[] EncabezadosDocumentosPagados =
    ["FOLIO FISCAL", "SERIE/FOLIO", "PARCIALIDAD", "SALDO ANTERIOR", "IMPORTE PAGADO", "SALDO INSOLUTO"];

    private static void DocumentosPagados(IContainer contenedor, List<DocumentoPagado> documentos, int decimales)
        => contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(columnas =>
            {
                columnas.RelativeColumn(2);   // folio fiscal
                columnas.ConstantColumn(55);  // serie/folio
                columnas.ConstantColumn(55);  // parcialidad
                columnas.ConstantColumn(65);  // saldo anterior
                columnas.ConstantColumn(65);  // importe pagado
                columnas.ConstantColumn(65);  // saldo insoluto
            });

            tabla.Header(encabezado =>
            {
                foreach (var titulo in EncabezadosDocumentosPagados)
                    encabezado.Cell().Background(EstiloDeComprobante.Azul).Padding(4).Text(titulo).FontColor(EstiloDeComprobante.Blanco).Bold().FontSize(7);
            });

            foreach (var doc in documentos)
            {
                tabla.Cell().Padding(3).Text(doc.IdDocumento.ToString().ToUpperInvariant()).FontSize(7);
                tabla.Cell().Padding(3).Text(FolioDeDocumento(doc));
                tabla.Cell().Padding(3).AlignCenter().Text(doc.NumParcialidad.ToString());
                tabla.Cell().Padding(3).AlignRight().Text(Cifra(doc.ImpSaldoAnt, decimales));
                tabla.Cell().Padding(3).AlignRight().Text(Cifra(doc.ImpPagado, decimales));
                tabla.Cell().Padding(3).AlignRight().Text(Cifra(doc.ImpSaldoInsoluto, decimales));

                if (doc.Impuestos.Count == 0) continue;

                // Misma idea que en Conceptos: el desglose de impuestos de este documento pagado,
                // proporcional a lo abonado en este pago (no al total de la factura original).
                tabla.Cell().ColumnSpan(6).PaddingLeft(15).PaddingBottom(3).Table(sub =>
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

                    foreach (var impuesto in doc.Impuestos)
                    {
                        sub.Cell().Padding(2).Text(Cifra(impuesto.Base, decimales)).FontSize(7);
                        sub.Cell().Padding(2).Text(NombreDeImpuesto(impuesto.Impuesto, impuesto.EsRetencion)).FontSize(7);
                        sub.Cell().Padding(2).Text(impuesto.TipoFactor).FontSize(7);
                        sub.Cell().Padding(2).Text(impuesto.TasaOCuota is { } t ? Cifra(t, 6) : "—").FontSize(7);
                        sub.Cell().Padding(2).Text(impuesto.Importe is { } i ? Cifra(i, decimales) : "—").FontSize(7);
                    }
                });
            }
        });

    private static string FolioDeDocumento(DocumentoPagado doc)
        => doc.Serie is null && doc.Folio is null ? "—" : $"{doc.Serie}{doc.Folio}";
}
