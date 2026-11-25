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

/// Lo que el PDF necesita y no está en el comprobante. Logo: ya descifrado; nulo si no hay.
/// LeyendaDelTimbre: el atributo Leyenda del timbre fiscal; con el PAC simulado dice que no
/// tiene validez fiscal.
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
    public Facturacion.Shared.Obras.ServicioParcialConstruccionDto? ServicioParcial { get; init; }

    // No viene del XML: las deducciones de obra no están en el CFDI. Se congelan al timbrar
    // porque la pestaña Obra solo se edita en borrador.
    public Facturacion.Shared.Obras.DatosObraDto? EstimacionObra { get; init; }
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
        ["CANTIDAD", "CLAVE\nUNIDAD", "CLAVE DEL\nPRODUCTO", "DESCRIPCIÓN", "OBJ.\nIMP.", "P. UNITARIO", "IMPORTE"];

    private const float AnchoUnitario = 72;
    private const float AnchoImporte = 76;
    private const float AnchoFechas = 154;

    public byte[] Generar(Comprobante comprobante, DatosDelPdf datos)
        => Facturacion.Server.Infra.Pdf.CandadoDePdf.Generar(Construir(comprobante, datos));

    public IDocument Construir(Comprobante comprobante, DatosDelPdf datos)
    {
        // El QR lleva el total como está en el XML: en un pago es «0» (moneda XXX), y con decimales
        // el SAT no encontraría el comprobante.
        var qr = datos.EsBorrador ? null : Qr(comprobante, comprobante.TipoDeComprobante == "P" ? 0 : datos.Decimales);

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
                    if (!datos.EsBorrador)
                        pie.Item().Element(e => Pie(e, comprobante, qr));
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
        => contenedor.Column(columna =>
        {
            if (!datos.EsBorrador && !string.IsNullOrWhiteSpace(datos.LeyendaDelTimbre))
                columna.Item().PaddingBottom(5).Text(datos.LeyendaDelTimbre).FontSize(7).Bold();
            if (!datos.EsBorrador && c.Estatus is ("cancelado" or "en_cancelacion"))
                columna.Item().PaddingBottom(5).Element(e => EstiloDeComprobante.Seccion(e,
                    c.Estatus == "cancelado" ? "CFDI CANCELADO" : "CFDI EN PROCESO DE CANCELACIÓN"));

            columna.Item().PaddingBottom(4).Row(fila =>
            {
                if (datos.Logo is { Length: > 0 } logo)
                    fila.ConstantItem(124).AlignLeft().AlignMiddle().Width(108).Height(78).Image(logo).FitArea();
                fila.RelativeItem().Column(emisor =>
                {
                    emisor.Item().AlignRight().Text(c.EmisorNombre).Bold().FontSize(16).FontColor(EstiloDeComprobante.Tinta);
                    emisor.Item().PaddingTop(12).AlignRight().Text($"C.P. de expedición: {c.LugarExpedicion}")
                        .FontColor(EstiloDeComprobante.Tinta);
                    emisor.Item().PaddingTop(6).AlignRight().Text($"Régimen fiscal: {c.EmisorRegimenFiscal}");
                    emisor.Item().PaddingTop(3).Row(datosEmisor =>
                    {
                        datosEmisor.RelativeItem().AlignCenter().Text(TituloDelTipo(c.TipoDeComprobante)).Bold().FontSize(11);
                        datosEmisor.ConstantItem(AnchoFechas).AlignRight().Text($"R.F.C. {c.EmisorRfc}").Bold().FontSize(10);
                    });
                });
            });
            columna.Item().Table(tabla =>
            {
                tabla.ColumnsDefinition(x =>
                {
                    x.ConstantColumn(47); x.ConstantColumn(66); x.RelativeColumn(1.55f);
                    x.RelativeColumn(); x.RelativeColumn(1.25f);
                });
                foreach (var titulo in new[] { "Versión", "Folio interno", "Folio fiscal", "No. de serie del CSD SAT", "No. de serie del CSD emisor" })
                    EstiloDeComprobante.Cabecera(tabla.Cell()).Text(titulo);
                foreach (var valor in new[] { "4.0", datos.EsBorrador ? "" : FolioInterno(c),
                    datos.EsBorrador ? "" : c.Uuid?.ToString().ToUpperInvariant() ?? "",
                    datos.EsBorrador ? "" : c.NoCertificadoSat ?? "",
                    datos.EsBorrador ? "" : c.NoCertificadoEmisor ?? "" })
                    EstiloDeComprobante.Celda(tabla.Cell()).Text(valor).FontSize(7);
            });
        });

    internal static void Receptor(IContainer contenedor, Comprobante c, DatosDelPdf datos)
        => contenedor.BorderBottom(0.5f).BorderColor(EstiloDeComprobante.Borde).Row(fila =>
        {
            fila.RelativeItem().PaddingRight(8).Column(cliente =>
            {
                cliente.Item().Row(r =>
                {
                    r.ConstantItem(62).Element(e => EstiloDeComprobante.Seccion(e, "Cliente"));
                    r.RelativeItem().Padding(4).Text(c.ReceptorNombre).Bold();
                });
                cliente.Item().PaddingLeft(66).PaddingTop(3).Text($"Domicilio fiscal: C.P. {c.ReceptorDomicilioFiscal}");
                cliente.Item().PaddingTop(12).Element(e => EtiquetaYValor(e, "Uso del CFDI", c.ReceptorUsoCfdi, 66));
                cliente.Item().PaddingVertical(3).Element(e => EtiquetaYValor(e, "Régimen fiscal", c.ReceptorRegimenFiscal, 66));
                if (!string.IsNullOrWhiteSpace(c.ReceptorNumRegIdTrib))
                    cliente.Item().PaddingTop(3).Text($"Registro tributario: {c.ReceptorNumRegIdTrib} · {c.ReceptorResidenciaFiscal}");
                cliente.Item().BorderTop(0.5f).BorderColor(EstiloDeComprobante.Borde).Row(lugar =>
                {
                    lugar.ConstantItem(94).Element(e => EstiloDeComprobante.Seccion(e, "Lugar de expedición"));
                    lugar.RelativeItem().AlignMiddle().PaddingLeft(4).Text(c.LugarExpedicion);
                });
            });
            fila.ConstantItem(AnchoFechas).Column(detalle =>
            {
                detalle.Item().Row(r =>
                {
                    r.ConstantItem(42).Element(e => EstiloDeComprobante.Seccion(e, "R.F.C."));
                    r.RelativeItem().Padding(4).Text(c.ReceptorRfc);
                });
                detalle.Item().Element(e => EstiloDeComprobante.Seccion(e, "Fecha y hora de certificación"));
                var certificacion = !datos.EsBorrador && c.FechaTimbradoUtc is { } fecha
                    ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fecha, DateTimeKind.Utc), datos.ZonaHoraria ?? TimeZoneInfo.Utc)
                        .ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture) : "";
                detalle.Item().MinHeight(17).Padding(4).Text(certificacion);
                detalle.Item().Element(e => EstiloDeComprobante.Seccion(e, "Fecha y hora de emisión"));
                detalle.Item().Padding(4).Text(datos.FechaLocal.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture));
            });
        });

    private static void Cuerpo(IContainer contenedor, Comprobante c, DatosDelPdf datos)
        => contenedor.Column(columna =>
        {
            columna.Item().Element(e => Receptor(e, c, datos));
            if (c.TipoDeComprobante == "P")
                columna.Item().PaddingTop(5).Element(e => ComplementoDePago(e, c.Pagos, datos.Decimales,
                    datos.ZonaHoraria ?? throw new InvalidOperationException("Falta el huso horario del PDF de pagos.")));
            else
            {
                columna.Item().Element(e => Conceptos(e, c, datos.Decimales));
                if (datos.EstimacionObra is { } estimacion)
                    columna.Item().Element(e => TotalesDeObra(e, c, estimacion, datos.Decimales));
                else
                    columna.Item().Element(e => Totales(e, c, datos.Decimales, datos.ImpuestosLocales ?? []));
                if (datos.Notaria is { } notaria)
                    columna.Item().PaddingTop(10).Element(e => SeccionNotarial(e, notaria, datos.Decimales));
            }
            if (datos.ComercioExterior is { } comercio)
                columna.Item().PaddingTop(10).Element(e => SeccionComercioExterior(e, c, comercio));
            if (datos.ServicioParcial is { } parcial)
            {
                columna.Item().PaddingTop(8).Element(e => EstiloDeComprobante.Seccion(e, "Servicios parciales de construcción"));
                columna.Item().Padding(4).Text($"Permiso: {parcial.NumeroPermiso}");
                var calle = string.Join(" ", new[] { parcial.Calle, parcial.NoExterior, parcial.NoInterior }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
                var direccion = string.Join(", ", new[] { calle, parcial.Colonia, parcial.Localidad,
                    parcial.Municipio, $"Estado {parcial.Estado}", $"C.P. {parcial.CodigoPostal}" }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
                columna.Item().Padding(4).Text(direccion);
                if (!string.IsNullOrWhiteSpace(parcial.Referencia)) columna.Item().Padding(4).Text(parcial.Referencia);
            }
            if (c.TipoDeComprobante == "P" && c.Relacionados.Count > 0)
            {
                columna.Item().PaddingTop(8).Element(e => EstiloDeComprobante.Seccion(e, "Pago sustituido · Relación 04"));
                foreach (var relacionado in c.Relacionados)
                    columna.Item().Padding(4).Text(relacionado.UuidRelacionado.ToString("D").ToUpperInvariant());
            }
            if (c.TipoDeComprobante == "E" && c.Relacionados.Count > 0)
            {
                columna.Item().PaddingTop(8).Element(e => EstiloDeComprobante.Seccion(e, "Factura relacionada"));
                foreach (var relacionado in c.Relacionados)
                    columna.Item().Padding(4).Text($"Relación {relacionado.TipoRelacion} · {relacionado.UuidRelacionado:D}");
            }
            if (!string.IsNullOrWhiteSpace(c.Observaciones))
            {
                columna.Item().PaddingTop(8).Element(e => EstiloDeComprobante.Seccion(e, "Observaciones"));
                columna.Item().Padding(4).Text(c.Observaciones);
            }
        });

    private static void SeccionComercioExterior(
        IContainer contenedor, Comprobante comprobante, DatosComercioExteriorDto datos)
        => contenedor.Column(columna =>
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
        => contenedor.Column(columna =>
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
                columna.Item().Text("—").FontSize(7).Light();
            foreach (var persona in datos.Enajenantes)
                columna.Item().PaddingTop(2).Element(e => ParteNotarial(e, persona));

            columna.Item().PaddingTop(6).Text("Adquirentes").Bold();
            if (datos.Adquirentes.Count == 0)
                columna.Item().Text("—").FontSize(7).Light();
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
                {
                    var celda = h.Cell().BorderRight(0.4f).BorderColor(EstiloDeComprobante.Borde);
                    var cabecera = EstiloDeComprobante.Cabecera(celda).AlignMiddle();
                    (titulo == "DESCRIPCIÓN" ? cabecera : cabecera.AlignCenter()).Text(titulo).FontSize(6.5f);
                }
            });
            foreach (var concepto in c.Conceptos.OrderBy(x => x.Orden))
            {
                tabla.Cell().ColumnSpan(7).PreventPageBreak().Table(renglon =>
                {
                    renglon.ColumnsDefinition(ColumnasDeConceptos);
                    EstiloDeComprobante.Celda(renglon.Cell()).AlignRight().Text(concepto.Cantidad.ToString("#,0.00####", CultureInfo.InvariantCulture));
                    EstiloDeComprobante.Celda(renglon.Cell()).AlignCenter().Text(concepto.ClaveUnidad);
                    EstiloDeComprobante.Celda(renglon.Cell()).Text(concepto.ClaveProdServ);
                    EstiloDeComprobante.Celda(renglon.Cell()).Element(e => DetalleDeConcepto(e, concepto));
                    EstiloDeComprobante.Celda(renglon.Cell()).AlignCenter().Text(concepto.ObjetoImp);
                    EstiloDeComprobante.Celda(renglon.Cell()).AlignRight().Text(concepto.ValorUnitario.ToString("#,0.00####", CultureInfo.InvariantCulture));
                    renglon.Cell().BorderBottom(0.4f).BorderColor(EstiloDeComprobante.Borde).Padding(4)
                        .AlignRight().Text(Cifra(concepto.Importe, decimales));
                });
            }
        });

    private static void DetalleDeConcepto(IContainer contenedor, Concepto concepto)
        => contenedor.Column(detalle =>
        {
            detalle.Item().Text(concepto.Descripcion);
            if (Emision.DatosFiscalesDeConcepto.Leer(concepto.DatosFiscalesJson) is { } fiscales)
            {
                foreach (var pedimento in fiscales.Pedimentos) detalle.Item().Text($"Pedimento: {pedimento}").FontSize(6.5f);
                foreach (var cuenta in fiscales.CuentasPrediales) detalle.Item().Text($"Cuenta predial: {cuenta}").FontSize(6.5f);
                if (fiscales.Tercero is { } tercero) detalle.Item().Text($"A cuenta de {tercero.Rfc} · {tercero.Nombre}").FontSize(6.5f);
            }
            if (concepto.Impuestos.Count == 0) return;

            detalle.Item().PaddingTop(4).Table(tabla =>
            {
                tabla.ColumnsDefinition(x =>
                {
                    x.RelativeColumn(1.25f); x.RelativeColumn(); x.RelativeColumn(); x.RelativeColumn(); x.RelativeColumn(1.25f);
                });
                foreach (var titulo in new[] { "BASE", "IMPUESTO", "TIPO FACTOR", "TASA O CUOTA", "IMPORTE" })
                    tabla.Cell().BorderBottom(0.4f).Padding(1).AlignCenter().Text(titulo).Bold().FontSize(6);
                foreach (var impuesto in concepto.Impuestos)
                {
                    tabla.Cell().Padding(1).AlignRight().Text(Cifra(impuesto.Base, 6)).FontSize(6.5f);
                    tabla.Cell().Padding(1).AlignCenter().Text(NombreDeImpuesto(impuesto.Impuesto, impuesto.EsRetencion)).FontSize(6.5f);
                    tabla.Cell().Padding(1).AlignCenter().Text(impuesto.TipoFactor).FontSize(6.5f);
                    tabla.Cell().Padding(1).AlignRight().Text(impuesto.TasaOCuota is { } t ? Cifra(t, 6) : "-").FontSize(6.5f);
                    tabla.Cell().Padding(1).AlignRight().Text(impuesto.Importe is { } i ? Cifra(i, 6) : "-").FontSize(6.5f);
                }
            });
        });

    private static void ColumnasDeConceptos(TableColumnsDefinitionDescriptor columnas)
    {
        columnas.ConstantColumn(43); columnas.ConstantColumn(44); columnas.ConstantColumn(55); columnas.RelativeColumn();
        columnas.ConstantColumn(27); columnas.ConstantColumn(AnchoUnitario); columnas.ConstantColumn(AnchoImporte);
    }

    private static void Totales(IContainer contenedor, Comprobante c, int decimales, IReadOnlyList<ImpuestoLocalCfdi> locales)
        => contenedor.PreventPageBreak().PaddingTop(6).BorderTop(0.5f).BorderColor(EstiloDeComprobante.Borde).Column(bloque =>
        {
            bloque.Item().PaddingTop(4).Row(fila =>
            {
                fila.RelativeItem().PaddingTop(4).PaddingRight(12).Element(e => ColumnaMetodoDePago(e, c));
                fila.ConstantItem(210).PaddingTop(2).PaddingHorizontal(4).Column(totales =>
                {
                    totales.Item().Element(e => Dato(e, "SUBTOTAL:", Cifra(c.SubTotal, decimales), negrita: true, anchoDelValor: 76));
                    if (c.Descuento > 0)
                        totales.Item().PaddingTop(2).Element(e => Dato(e, "DESCUENTO:", Cifra(c.Descuento, decimales), negrita: true, anchoDelValor: 76));
                    foreach (var retencion in new[] { false, true })
                    {
                        var total = retencion ? c.TotalImpuestosRetenidos : c.TotalImpuestosTrasladados;
                        if (total <= 0) continue;
                        var grupos = c.Conceptos.SelectMany(x => x.Impuestos)
                            .Where(x => x.EsRetencion == retencion && x.Importe is not null)
                            .GroupBy(x => (x.Impuesto, x.TipoFactor, x.TasaOCuota)).ToArray();
                        if (grupos.Length > 0 && decimal.Round(grupos.Sum(g => g.Sum(x => x.Importe!.Value)), decimales) == decimal.Round(total, decimales))
                            foreach (var grupo in grupos)
                                totales.Item().PaddingTop(2).Element(e => Dato(e,
                                    NombreDeImpuesto(grupo.Key.Impuesto, retencion) + (grupo.Key.TasaOCuota is { } tasa && grupo.Key.TipoFactor == "Tasa" ? $" {tasa * 100:0.##} %:" : ":"),
                                    Cifra(grupo.Sum(x => x.Importe!.Value), decimales), negrita: true, anchoDelValor: 76));
                        else
                            totales.Item().PaddingTop(2).Element(e => Dato(e, retencion ? "IMPUESTOS RETENIDOS:" : "IMPUESTOS TRASLADADOS:",
                                Cifra(total, decimales), negrita: true, anchoDelValor: 76));
                    }
                    foreach (var local in locales)
                        totales.Item().PaddingTop(2).Element(e => Dato(e,
                            $"{local.Nombre} ({Cifra(local.Tasa, 2)} %):",
                            (local.EsRetencion ? "−" : "") + Cifra(local.Importe, 2), negrita: true, anchoDelValor: 76));
                });
            });
            bloque.Item().PaddingTop(6).Element(e => FranjaCantidadConLetraYTotal(e, c.Moneda, $"TOTAL {c.Moneda}:", c.Total, decimales));
        });

    // ── Factura de obra: desglose completo de estimación hasta importe líquido ──────────

    private static void TotalesDeObra(IContainer contenedor, Comprobante c,
        Facturacion.Shared.Obras.DatosObraDto estimacion, int decimales)
        => contenedor.PreventPageBreak().PaddingTop(6).BorderTop(0.5f).BorderColor(EstiloDeComprobante.Borde).Column(bloque =>
        {
            var deduccionesActivas = estimacion.Deducciones.Where(d => d.Porcentaje != 0).ToArray();
            var totalDeducciones = deduccionesActivas.Sum(d => d.Importe);
            var etiquetaIva = EtiquetaDeIvaDeObra(c);

            bloque.Item().PaddingTop(4).Row(fila =>
            {
                fila.RelativeItem().PaddingTop(4).PaddingRight(12).Element(e => ColumnaMetodoDePago(e, c));
                fila.ConstantItem(230).PaddingTop(2).PaddingHorizontal(4).Column(totales =>
                {
                    totales.Item().Element(e => Dato(e, "IMPORTE DE LOS TRABAJOS:", Cifra(estimacion.ImporteTrabajos, decimales), negrita: true, anchoDelValor: 76));
                    totales.Item().PaddingTop(2).Element(e => Dato(e, "AMORTIZACIÓN DEL ANTICIPO:", Cifra(estimacion.Amortizacion, decimales), negrita: true, anchoDelValor: 76));
                    totales.Item().PaddingTop(2).Element(e => Dato(e, "RETENCIONES:", Cifra(estimacion.Retenciones, decimales), negrita: true, anchoDelValor: 76));
                    totales.Item().PaddingTop(2).Element(e => Dato(e, "DEVOLUCIONES:", Cifra(estimacion.Devoluciones, decimales), negrita: true, anchoDelValor: 76));
                    totales.Item().PaddingTop(2).Element(e => Dato(e, "SUBTOTAL:", Cifra(estimacion.SubtotalEstimacion, decimales), negrita: true, anchoDelValor: 76));
                    totales.Item().PaddingTop(2).Element(e => Dato(e, etiquetaIva, Cifra(estimacion.IvaEstimado, decimales), negrita: true, anchoDelValor: 76));
                    totales.Item().PaddingTop(2).Element(e => Dato(e, "TOTAL:", Cifra(estimacion.TotalEstimacion, decimales), negrita: true, anchoDelValor: 76));
                    foreach (var deduccion in deduccionesActivas)
                        totales.Item().PaddingTop(2).Element(e => Dato(e, deduccion.Nombre, Cifra(deduccion.Importe, decimales), negrita: true, anchoDelValor: 76));
                    totales.Item().PaddingTop(2).Element(e => Dato(e, "TOTAL DEDUCCIONES:", Cifra(totalDeducciones, decimales), negrita: true, anchoDelValor: 76));
                });
            });
            bloque.Item().PaddingTop(6).Element(e => FranjaCantidadConLetraYTotal(e, c.Moneda, "IMPORTE LÍQUIDO:", estimacion.ImporteLiquido, decimales));
        });

    private static void ColumnaMetodoDePago(IContainer contenedor, Comprobante c)
        => contenedor.Column(pago =>
        {
            var metodo = c.MetodoPago switch
            {
                "PUE" => "PUE PAGO EN UNA SOLA EXHIBICIÓN",
                "PPD" => "PPD PAGO EN PARCIALIDADES O DIFERIDO",
                _ => c.MetodoPago ?? "-"
            };
            pago.Item().Element(e => EtiquetaAzulYValor(e, "Método de Pago", metodo));
            pago.Item().PaddingTop(5).Element(e => EtiquetaAzulYValor(e, "Forma de Pago", c.FormaPago ?? "-"));
            if (c.Moneda != "MXN" || c.TipoCambio is not null)
                pago.Item().PaddingTop(5).Element(e => EtiquetaAzulYValor(e, "Moneda / T.C.",
                    $"{c.Moneda} · {(c.TipoCambio is { } tc ? Cifra(tc, 6) : "1")}"));
            if (!string.IsNullOrWhiteSpace(c.CondicionesDePago))
                pago.Item().PaddingTop(5).Text($"Condiciones de pago: {c.CondicionesDePago}").FontSize(7.5f);
        });

    private static void EtiquetaAzulYValor(IContainer contenedor, string etiqueta, string valor)
        => contenedor.Row(fila =>
        {
            fila.ConstantItem(82).Background(EstiloDeComprobante.Azul).PaddingVertical(3).PaddingHorizontal(5)
                .Text(etiqueta).Bold().FontSize(7.5f).FontColor(EstiloDeComprobante.Blanco);
            fila.RelativeItem().PaddingLeft(6).AlignMiddle().Text(valor).FontSize(7.5f);
        });

    private static void FranjaCantidadConLetraYTotal(
        IContainer contenedor, string moneda, string etiquetaTotal, decimal importeTotal, int decimales)
        => contenedor.BorderTop(0.5f).BorderColor(EstiloDeComprobante.Borde).PaddingTop(3).Row(fila =>
        {
            fila.RelativeItem().PaddingRight(8).Row(letra =>
            {
                letra.ConstantItem(82).Background(EstiloDeComprobante.Azul).PaddingVertical(4).PaddingHorizontal(5)
                    .AlignMiddle().Text("Cantidad con letra").Bold().FontSize(7.5f).FontColor(EstiloDeComprobante.Blanco);
                letra.RelativeItem().PaddingLeft(6).AlignMiddle()
                    .Text(ImporteEnLetra.Convertir(importeTotal, moneda)).FontSize(7.5f);
            });
            fila.ConstantItem(230).Row(total =>
            {
                total.RelativeItem().Background(EstiloDeComprobante.Azul).PaddingVertical(4).PaddingHorizontal(6)
                    .AlignMiddle().AlignRight().Text(etiquetaTotal).Bold().FontSize(8.5f).FontColor(EstiloDeComprobante.Blanco);
                total.ConstantItem(80).PaddingLeft(4).AlignMiddle().AlignRight()
                    .Text(Cifra(importeTotal, decimales)).Bold().FontSize(9);
            });
        });

    private static string EtiquetaDeIvaDeObra(Comprobante c)
    {
        var tasas = c.Conceptos.SelectMany(x => x.Impuestos)
            .Where(x => !x.EsRetencion && x.Impuesto == "002" && x.TipoFactor == "Tasa" && x.TasaOCuota is not null)
            .Select(x => x.TasaOCuota!.Value)
            .Distinct()
            .ToArray();
        return tasas.Length == 1 ? $"IVA {tasas[0] * 100:0.##} %:" : "IVA:";
    }

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

    private static void EtiquetaYValor(IContainer contenedor, string etiqueta, string valor, float anchoEtiqueta)
        => contenedor.Row(fila =>
        {
            fila.ConstantItem(anchoEtiqueta).Text(etiqueta).Bold();
            fila.RelativeItem().Text(valor);
        });

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

    // Más angosto que el de totales: caben tres pares por renglón sin partir las etiquetas.
    private const float AnchoDeValorDelPago = 90;

    private static void ComplementoDePago(IContainer contenedor, List<Pago> pagos, int decimales, TimeZoneInfo zona)
        => contenedor.Column(columna =>
        {
            foreach (var pago in pagos)
            {
                columna.Item().PaddingBottom(6).Border(0.5f).Padding(5).Column(bloque =>
                {
                    bloque.Item().Element(e => EstiloDeComprobante.Seccion(e, "DATOS DEL PAGO"));

                    // Sin separación, el valor de una columna queda pegado a la etiqueta de la siguiente.
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

                    // Los datos bancarios son opcionales: solo se muestran si vienen capturados.
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

                    if (pago.TipoCadPago == "01")
                        bloque.Item().PaddingTop(3).Text("Cadena de pago: 01 — SPEI").FontSize(7);
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
                Importe: g.Key.TipoFactor == TiposDeFactor.Exento ? (decimal?)null : g.Sum(i => i.Importe ?? 0m)))
            .OrderBy(t => t.Etiqueta);

        columna.Item().PaddingTop(3).Element(e => EstiloDeComprobante.Seccion(e, "TOTALES DEL COMPLEMENTO (MXN)"));

        foreach (var (etiqueta, importe) in retenciones)
        {
            columna.Item().Element(e => Dato(e, $"Retención {etiqueta}", Cifra(importe, decimales)));
        }

        foreach (var (etiqueta, baseImportes, importe) in traslados)
        {
            if (importe <= 0 && baseImportes <= 0) continue;
            columna.Item().Element(e => Dato(e, $"Traslado {etiqueta} (base {Cifra(baseImportes, decimales)})",
                importe is { } valor ? Cifra(valor, decimales) : "—"));
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
                tabla.Cell().Padding(3).Text($"{FolioDeDocumento(doc)} · {doc.MonedaDR}\nEquiv. {doc.EquivalenciaDR:0.##########}");
                tabla.Cell().Padding(3).AlignCenter().Text(doc.NumParcialidad.ToString());
                tabla.Cell().Padding(3).AlignRight().Text(Cifra(doc.ImpSaldoAnt, doc.DecimalesMonedaDR));
                tabla.Cell().Padding(3).AlignRight().Text(Cifra(doc.ImpPagado, doc.DecimalesMonedaDR));
                tabla.Cell().Padding(3).AlignRight().Text(Cifra(doc.ImpSaldoInsoluto, doc.DecimalesMonedaDR));

                if (doc.Impuestos.Count == 0) continue;

                // Desglose proporcional a lo abonado en este pago, no al total de la factura.
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
                        sub.Cell().Padding(2).Text(Cifra(impuesto.Base, doc.DecimalesMonedaDR)).FontSize(7);
                        sub.Cell().Padding(2).Text(NombreDeImpuesto(impuesto.Impuesto, impuesto.EsRetencion)).FontSize(7);
                        sub.Cell().Padding(2).Text(impuesto.TipoFactor).FontSize(7);
                        sub.Cell().Padding(2).Text(impuesto.TasaOCuota is { } t ? Cifra(t, 6) : "—").FontSize(7);
                        sub.Cell().Padding(2).Text(impuesto.Importe is { } i ? Cifra(i, doc.DecimalesMonedaDR) : "—").FontSize(7);
                    }
                });
            }
        });

    private static string FolioDeDocumento(DocumentoPagado doc)
        => doc.Serie is null && doc.Folio is null ? "—" : $"{doc.Serie}{doc.Folio}";
}
