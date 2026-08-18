using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Salidas;

namespace Facturacion.Pruebas;

/// <summary>
/// Representación impresa del CFDI (fase B5): la expresión del código QR y el PDF.
///
/// <para><b>De dónde sale el caso de la expresión</b></para>
/// El ejemplo que ancla estas pruebas es el que publican las implementaciones de referencia
/// de la comunidad mexicana (<c>phpcfdi/cfdi-expresiones</c>, <c>nodecfdi</c>,
/// <c>CfdiUtils</c>), no uno inventado con mi propio código. El orden importa: si el caso
/// saliera de la misma función que se prueba, pasaría aunque el formato estuviera mal.
/// </summary>
public sealed class RepresentacionImpresaPruebas
{
    static RepresentacionImpresaPruebas()
        => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    // ── La expresión del QR ─────────────────────────────────────────────────────────────

    [Fact]
    public void La_expresion_coincide_con_el_ejemplo_de_referencia()
    {
        var expresion = ExpresionImpresa.Construir(
            Guid.Parse("CEE4BE01-ADFA-4DEB-8421-ADD60F0BEDAC"),
            "POT9207213D6",
            "DIM8701081LA",
            "2010.01",
            // Los últimos ocho caracteres son lo único que entra; el resto del sello sobra.
            "cualquierSelloLargoEnBase64QueTermineEn/OAgdg==");

        Assert.Equal(
            "https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx" +
            "?id=CEE4BE01-ADFA-4DEB-8421-ADD60F0BEDAC&re=POT9207213D6&rr=DIM8701081LA&tt=2010.01&fe=/OAgdg==",
            expresion);
    }

    /// <summary>
    /// El error que rompe la verificación en silencio: escapar <c>fe</c> para URL. Lleva
    /// <c>/</c> y <c>=</c> en base 64 y tienen que ir crudos.
    /// </summary>
    [Fact]
    public void El_sello_no_se_codifica_para_url()
    {
        var expresion = ExpresionImpresa.Construir(
            Guid.NewGuid(), "EKU9003173C9", "XAXX010101000", "116.00", "abcdef/OAgdg==")!;

        Assert.EndsWith("&fe=/OAgdg==", expresion, StringComparison.Ordinal);
        Assert.DoesNotContain("%2F", expresion, StringComparison.Ordinal);
        Assert.DoesNotContain("%3D", expresion, StringComparison.Ordinal);
    }

    /// <summary>
    /// En CFDI 3.3 el total se rellenaba a 18 enteros y 6 decimales; en 4.0 no. Arrastrar el
    /// formato viejo hace que el SAT no encuentre el comprobante.
    /// </summary>
    [Fact]
    public void El_total_va_sin_relleno_de_ceros()
        => Assert.Equal("11.58", ExpresionImpresa.Total(11.58m, 2));

    [Fact]
    public void El_uuid_va_en_mayusculas()
        => Assert.Contains(
            "id=CEE4BE01-ADFA-4DEB-8421-ADD60F0BEDAC",
            ExpresionImpresa.Construir(
                Guid.Parse("cee4be01-adfa-4deb-8421-add60f0bedac"),
                "EKU9003173C9", "XAXX010101000", "1.00", "12345678")!,
            StringComparison.Ordinal);

    /// <summary>
    /// Un borrador no tiene UUID ni sello. Devolver una expresión a medias pondría en el PDF
    /// un QR que lleva a una página de error del SAT, que es peor que no ponerlo.
    /// </summary>
    [Theory]
    [InlineData(false, "sello")]
    [InlineData(true, null)]
    [InlineData(true, "")]
    public void Sin_uuid_o_sin_sello_no_hay_expresion(bool hayUuid, string? sello)
        => Assert.Null(ExpresionImpresa.Construir(
            hayUuid ? Guid.NewGuid() : null, "EKU9003173C9", "XAXX010101000", "1.00", sello));

    // ── El PDF ──────────────────────────────────────────────────────────────────────────

    private static Comprobante Comprobante(bool timbrado)
    {
        var c = new Comprobante
        {
            Estatus = timbrado ? "timbrado" : "borrador",
            Serie = "A",
            Folio = timbrado ? 123 : null,
            TipoDeComprobante = "I",
            FechaEmisionUtc = new DateTime(2026, 8, 17, 18, 0, 0, DateTimeKind.Utc),
            LugarExpedicion = "42000",
            Moneda = "MXN",
            MetodoPago = "PUE",
            FormaPago = "01",
            Exportacion = "01",
            EmisorRfc = "EKU9003173C9",
            EmisorNombre = "ESCUELA KEMPER URGATE",
            EmisorRegimenFiscal = "601",
            ReceptorRfc = "XAXX010101000",
            ReceptorNombre = "PUBLICO EN GENERAL",
            ReceptorRegimenFiscal = "616",
            ReceptorDomicilioFiscal = "42000",
            ReceptorUsoCfdi = "S01",
            SubTotal = 100m,
            TotalImpuestosTrasladados = 16m,
            Total = 116m,
            Conceptos =
            [
                new Concepto
                {
                    Orden = 1,
                    ClaveProdServ = "01010101",
                    ClaveUnidad = "H87",
                    Descripcion = "SERVICIO DE PRUEBA",
                    ObjetoImp = "02",
                    Cantidad = 1,
                    ValorUnitario = 100m,
                    Importe = 100m,
                    Impuestos =
                    [
                        new ImpuestoConcepto
                        {
                            Impuesto = "002", TipoFactor = "Tasa", TasaOCuota = 0.16m,
                            Base = 100m, Importe = 16m, EsRetencion = false
                        },
                        // Un exento junto al gravado: en el PDF tiene que salir sin tasa y
                        // sin importe, igual que en el XML.
                        new ImpuestoConcepto
                        {
                            Impuesto = "002", TipoFactor = "Exento", TasaOCuota = null,
                            Base = 100m, Importe = null, EsRetencion = true
                        }
                    ]
                }
            ]
        };

        if (!timbrado) return c;

        c.Uuid = Guid.Parse("CEE4BE01-ADFA-4DEB-8421-ADD60F0BEDAC");
        c.FechaTimbradoUtc = new DateTime(2026, 8, 17, 18, 1, 0, DateTimeKind.Utc);
        c.SelloCfd = "selloDelCfdiEnBase64/OAgdg==";
        c.SelloSat = "selloDelSatEnBase64==";
        c.CadenaOriginalSat = "||1.1|CEE4BE01|2026-08-17T12:01:00|...||";
        c.NoCertificadoEmisor = "30001000000400002434";
        c.NoCertificadoSat = "30001000000400002495";

        return c;
    }

    private static byte[] Pdf(Comprobante c, byte[]? logo = null)
        => new GeneradorDePdfCfdi().Generar(
            c, new DatosDelPdf(new DateTime(2026, 8, 17, 12, 0, 0), 2, logo));

    [Fact]
    public void Genera_un_pdf_valido_de_un_comprobante_timbrado()
    {
        var pdf = Pdf(Comprobante(timbrado: true));

        // %PDF- es la firma del formato; sin ella no es un PDF, por mucho que pese.
        Assert.Equal("%PDF-"u8.ToArray(), pdf.Take(5).ToArray());
        Assert.True(pdf.Length > 2000, $"El PDF salió sospechosamente corto: {pdf.Length} bytes.");
    }

    /// <summary>
    /// Un borrador se puede imprimir —el capturista quiere revisarlo antes de timbrar— pero
    /// sin QR: no hay nada que verificar todavía.
    /// </summary>
    [Fact]
    public void Un_borrador_se_imprime_pero_sin_codigo_qr()
    {
        var conQr = Pdf(Comprobante(timbrado: true));
        var sinQr = Pdf(Comprobante(timbrado: false));

        Assert.Equal("%PDF-"u8.ToArray(), sinQr.Take(5).ToArray());
        Assert.True(sinQr.Length < conQr.Length,
            "El borrador debería pesar menos que el timbrado, que además lleva el QR.");
    }

    [Fact]
    public void El_logo_de_la_empresa_entra_en_el_pdf()
    {
        // PNG mínimo de un pixel, para no depender de un archivo en disco.
        var logo = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        var conLogo = Pdf(Comprobante(timbrado: true), logo);
        var sinLogo = Pdf(Comprobante(timbrado: true));

        Assert.Equal("%PDF-"u8.ToArray(), conLogo.Take(5).ToArray());
        Assert.NotEqual(sinLogo.Length, conLogo.Length);
    }
}
