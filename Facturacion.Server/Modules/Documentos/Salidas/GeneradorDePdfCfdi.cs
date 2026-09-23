using System.Globalization;
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
public sealed record DatosDelPdf(
    DateTime FechaLocal,
    int Decimales,
    byte[]? Logo,
    bool EsBorrador = false,
    DatosNotarialesDelPdf? Notaria = null,
    DatosComercioExteriorDto? ComercioExterior = null);

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
        var qr = datos.EsBorrador ? null : Qr(comprobante, datos.Decimales);

        return Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.Letter);
                pagina.Margin(1.2f, Unit.Centimetre);
                pagina.DefaultTextStyle(t => t.FontSize(8).FontFamily(Fonts.Calibri));

                pagina.Header().Element(e => Encabezado(e, comprobante, datos));
                pagina.Content().Element(e => Cuerpo(e, comprobante, datos));
                pagina.Footer().Element(e => Pie(e, comprobante, qr, datos.EsBorrador));
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
                pagina.Footer().Element(e => Pie(e, comprobante, qr, datos.EsBorrador));
            });
        });
    }

    // ── Encabezado: emisor, logo y los identificadores fiscales ─────────────────────────

    private static void Encabezado(IContainer contenedor, Comprobante c, DatosDelPdf datos)
        => contenedor.PaddingBottom(8).Column(contenido =>
        {
            if (datos.EsBorrador)
            {
                contenido.Item()
                    .PaddingBottom(7)
                    .Border(1)
                    .Padding(5)
                    .AlignCenter()
                    .Text("BORRADOR — SIN VALIDEZ FISCAL")
                    .Bold()
                    .FontSize(10);
            }

            contenido.Item().Row(fila =>
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
                    var titulo = datos.EsBorrador
                        ? $"VISTA PREVIA · {TituloDelTipo(c.TipoDeComprobante)}"
                        : TituloDelTipo(c.TipoDeComprobante);

                    columna.Item().Text(titulo).Bold().FontSize(11).AlignCenter();
                    columna.Item().PaddingTop(3).Element(e => Dato(e, "Versión", "4.0"));
                    columna.Item().Element(e => Dato(e, "Folio interno", FolioInterno(c)));
                    columna.Item().Element(e => Dato(e, "Folio fiscal (UUID)",
                        datos.EsBorrador ? "No timbrado" : c.Uuid?.ToString().ToUpperInvariant() ?? "—"));
                    if (!datos.EsBorrador)
                        columna.Item().Element(e => Dato(e, "Estado del CFDI",
                            c.Estatus == "cancelado" ? "Cancelado" : "Vigente"));
                    columna.Item().Element(e => Dato(e, "No. serie CSD emisor",
                        datos.EsBorrador ? "No timbrado" : c.NoCertificadoEmisor ?? "—"));
                    columna.Item().Element(e => Dato(e, "No. serie CSD SAT",
                        datos.EsBorrador ? "No timbrado" : c.NoCertificadoSat ?? "—"));
                    columna.Item().Element(e => Dato(e, "Fecha de emisión", datos.FechaLocal.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)));
                    columna.Item().Element(e => Dato(e, "Fecha de certificación",
                        datos.EsBorrador ? "No timbrado" : c.FechaTimbradoUtc?.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture) ?? "—"));
                });
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

        if (c.TipoDeComprobante == "P")
        {
            columna.Item().PaddingTop(6).Element(e => ComplementoDePago(e, c.Pagos, datos.Decimales));
        }
        else
        {
            columna.Item().PaddingTop(6).Element(e => Conceptos(e, c, datos.Decimales));
            columna.Item().PaddingTop(6).Element(e => Totales(e, c, datos.Decimales));

            if (datos.Notaria is { } notaria)
                columna.Item().PaddingTop(10).Element(e => SeccionNotarial(e, notaria, datos.Decimales));
            }
            if (datos.ComercioExterior is { } comercio)
                columna.Item().PaddingTop(10).Element(e => SeccionComercioExterior(e, c, comercio));
        });

    private static void SeccionComercioExterior(
        IContainer contenedor, Comprobante comprobante, DatosComercioExteriorDto datos)
        => contenedor.Column(columna =>
        {
            columna.Item().PaddingBottom(4).Text("COMERCIO EXTERIOR 2.0").Bold().FontSize(9);
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
            columna.Item().PaddingBottom(4).Text("DATOS NOTARIALES").Bold().FontSize(9);
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
        => contenedor.Column(columna =>
        {
            columna.Item().Text(persona.NombreCompleto);
            columna.Item().Text($"RFC {persona.Rfc}" +
                (persona.Curp is null ? string.Empty : $"  ·  CURP {persona.Curp}") +
                (persona.Porcentaje is null ? string.Empty :
                    $"  ·  Participación {persona.Porcentaje.Value.ToString("F2", CultureInfo.InvariantCulture)} %"))
                .FontSize(7).Light();
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

    // ── Complemento de pago ─────────────────────────────────────────────────────────────

    private static void ComplementoDePago(IContainer contenedor, List<Pago> pagos, int decimales)
        => contenedor.Column(columna =>
        {
            foreach (var pago in pagos)
            {
                columna.Item().PaddingBottom(6).Border(0.5f).Padding(5).Column(bloque =>
                {
                    bloque.Item().Text("DATOS DEL PAGO").Bold();

                    bloque.Item().PaddingTop(3).Row(fila =>
                    {
                        fila.RelativeItem().Element(e => Dato(e, "Fecha de pago", pago.FechaPagoUtc.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)));
                        fila.RelativeItem().Element(e => Dato(e, "Forma de pago", pago.FormaDePagoP));
                        fila.RelativeItem().Element(e => Dato(e, "Moneda", pago.MonedaP));
                    });

                    bloque.Item().Row(fila =>
                    {
                        fila.RelativeItem().Element(e => Dato(e, "Monto", Cifra(pago.Monto, decimales)));
                        fila.RelativeItem().Element(e => Dato(e, "Tipo de cambio", pago.TipoCambioP is { } tc ? Cifra(tc, 6) : "—"));
                        fila.RelativeItem().Element(e => Dato(e, "No. de operación", pago.NumOperacion ?? "—"));
                    });

                    // Los datos bancarios son opcionales en el complemento: solo se muestran si vienen capturados.
                    if (pago.CtaOrdenante is not null || pago.CtaBeneficiario is not null)
                    {
                        bloque.Item().PaddingTop(2).Row(fila =>
                        {
                            fila.RelativeItem().Element(e => Dato(e, "Cuenta ordenante", pago.CtaOrdenante ?? "—"));
                            fila.RelativeItem().Element(e => Dato(e, "Banco ordenante", pago.RfcEmisorCtaOrd ?? pago.NomBancoOrdExt ?? "—"));
                            fila.RelativeItem().Element(e => Dato(e, "Cuenta beneficiaria", pago.CtaBeneficiario ?? "—"));
                        });
                    }

                    bloque.Item().PaddingTop(4).Element(e => DocumentosPagados(e, pago.Documentos, decimales));
                });
            }
            columna.Item().PaddingTop(4).Element(e => TotalesDelComplemento(e, pagos, decimales));
        });

    private static void TotalesDelComplemento(IContainer contenedor, List<Pago> pagos, int decimales)
    => contenedor.Column(columna =>
    {
        var impuestos = pagos
            .SelectMany(p => p.Documentos)
            .SelectMany(d => d.Impuestos)
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

        columna.Item().BorderTop(0.5f).PaddingTop(3).Text("TOTALES DEL COMPLEMENTO").Bold();

        foreach (var (etiqueta, importe) in retenciones)
        {
            if (importe <= 0) continue;
            columna.Item().Element(e => Dato(e, $"Retención {etiqueta}", Cifra(importe, decimales)));
        }

        foreach (var (etiqueta, baseImportes, importe) in traslados)
        {
            if (importe <= 0 && baseImportes <= 0) continue;
            columna.Item().Element(e => Dato(e, $"Traslado {etiqueta} (base {Cifra(baseImportes, decimales)})", Cifra(importe, decimales)));
        }

        columna.Item().PaddingTop(2).BorderTop(0.5f).Element(e =>
            Dato(e, "Monto total de pagos", Cifra(pagos.Sum(p => p.Monto), decimales), negrita: true));
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
                    encabezado.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(titulo).Bold().FontSize(7);
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
