using System.Xml.Linq;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.ComercioExterior;
using QuestPDF.Infrastructure;

namespace Facturacion.Pruebas;

public sealed class ConsistenciaFiscalYPdfFase3Pruebas
{
    private static readonly XNamespace Cfdi = "http://www.sat.gob.mx/cfd/4";
    private static readonly XNamespace Pago20 = "http://www.sat.gob.mx/Pagos20";
    private static readonly DatosDeEmision EmisionMxn =
        new(new DateTime(2026, 11, 23, 12, 0, 0), Decimales: 2, "30001000000400002434", "TUVCNjQ=");

    [Theory]
    [InlineData(0.0045, "0.0045")]
    [InlineData(1.234567, "1.234567")]
    [InlineData(100.5, "100.50")]
    [InlineData(100.0, "100.00")]
    public void Valor_unitario_conserva_hasta_seis_decimales_significativos(decimal valorUnitario, string esperado)
    {
        var comprobante = FacturaBase();
        comprobante.Conceptos[0].Cantidad = 1000m;
        comprobante.Conceptos[0].ValorUnitario = valorUnitario;
        comprobante.Conceptos[0].Importe = Math.Round(1000m * valorUnitario, 6);

        var xml = new GeneradorDeXmlCfdi().Generar(comprobante, EmisionMxn);
        var concepto = xml.Root!.Element(Cfdi + "Conceptos")!.Elements(Cfdi + "Concepto").First();

        Assert.Equal(esperado, concepto.Attribute("ValorUnitario")?.Value);
    }

    [Fact]
    public void Expresion_impresa_escapa_ampersand_en_rfc_y_conserva_fe_intacto()
    {
        var uuid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var url = ExpresionImpresa.Construir(uuid, "A&B010101AA0", "C&D020202BB1", "116.00", "ABCD+/12==");

        Assert.NotNull(url);
        Assert.Contains("&re=A%26B010101AA0", url);
        Assert.Contains("&rr=C%26D020202BB1", url);
        Assert.EndsWith("&fe=CD+/12==", url);
    }

    [Fact]
    public void Pagos_en_mxn_y_misma_moneda_emiten_uno_literal_en_tipo_cambio_y_equivalencia()
    {
        var pago = new Comprobante
        {
            Id = Guid.NewGuid(),
            Estatus = "borrador",
            TipoDeComprobante = "P",
            EmisorRfc = "AAA010101AAA",
            EmisorNombre = "EMISOR SA",
            EmisorRegimenFiscal = "601",
            LugarExpedicion = "06600",
            ReceptorRfc = "BBB010101BBB",
            ReceptorNombre = "RECEPTOR SA",
            ReceptorRegimenFiscal = "601",
            ReceptorDomicilioFiscal = "06600",
            ReceptorUsoCfdi = "CP01",
            Moneda = "XXX",
            Exportacion = "01",
            FechaEmisionUtc = new DateTime(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc),
            Pagos =
            [
                new Pago
                {
                    Id = Guid.NewGuid(),
                    FechaPagoUtc = new DateTime(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc),
                    FormaDePagoP = "03",
                    MonedaP = "MXN",
                    TipoCambioP = 1m,
                    Monto = 116m,
                    Documentos =
                    [
                        new DocumentoPagado
                        {
                            Id = Guid.NewGuid(),
                            IdDocumento = Guid.NewGuid(),
                            MonedaDR = "MXN",
                            DecimalesMonedaDR = 2,
                            EquivalenciaDR = 1m,
                            NumParcialidad = 1,
                            ImpSaldoAnt = 116m,
                            ImpPagado = 116m,
                            ImpSaldoInsoluto = 0m,
                            ObjetoImpDR = "01"
                        }
                    ]
                }
            ]
        };

        var xml = GeneradorDeXmlPago.Generar(pago, EmisionMxn with { ZonaHoraria = TimeZoneInfo.Utc });
        var nodoPago = xml.Root!.Descendants(Pago20 + "Pago").Single();
        var docto = nodoPago.Element(Pago20 + "DoctoRelacionado")!;

        Assert.Equal("1", nodoPago.Attribute("TipoCambioP")?.Value);
        Assert.Equal("1", docto.Attribute("EquivalenciaDR")?.Value);
    }

    [Fact]
    public async Task Generador_de_pdf_soporta_comercio_exterior_extenso_y_concurrencia()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var comprobante = FacturaBase();
        comprobante.Conceptos.Clear();
        var mercancias = new List<MercanciaComercioExteriorDto>();
        for (var i = 1; i <= 25; i++)
        {
            comprobante.Conceptos.Add(new Concepto
            {
                Id = Guid.NewGuid(),
                Orden = i,
                ClaveProdServ = "01010101",
                NoIdentificacion = $"SKU-{i:000}",
                Cantidad = 1m,
                ClaveUnidad = "H87",
                Descripcion = $"Mercancía de exportación número {i} con descripción suficientemente larga para ocupar varias líneas",
                ValorUnitario = 100m,
                Importe = 100m,
                ObjetoImp = "01"
            });
            mercancias.Add(new MercanciaComercioExteriorDto(
                OrdenConcepto: i,
                ClaveProdServConcepto: "01010101",
                DescripcionConcepto: $"Mercancía {i}",
                FraccionArancelaria: "0101210100",
                UnidadAduana: "06",
                CantidadAduana: 1m,
                ValorUnitarioAduana: 5m,
                ValorDolares: 5m,
                Marca: "MARCA",
                Modelo: $"MOD-{i}",
                Submodelo: null,
                NumeroSerie: $"SER-{i:0000}"));
        }

        var domicilio = new DomicilioComercioExteriorDto("Calle 1", "10", null, "Centro", null, null, "001", "JAL", "MEX", "44100");
        var comercio = new DatosComercioExteriorDto(
            ClavePedimento: "A1",
            CertificadoOrigen: false,
            NumeroCertificadoOrigen: null,
            NumeroExportadorConfiable: null,
            Incoterm: "FOB",
            TipoCambioUsd: 20m,
            TotalUsd: 125m,
            Observaciones: null,
            CurpEmisor: null,
            DomicilioEmisor: domicilio,
            DomicilioReceptor: domicilio with { Pais = "USA", Estado = "TX", CodigoPostal = "73301" },
            ResidenciaFiscal: "USA",
            NumeroRegistroTributario: "TAX123456",
            Mercancias: mercancias);

        var datos = new DatosDelPdf(
            FechaLocal: new DateTime(2026, 11, 23, 12, 0, 0),
            Decimales: 2,
            Logo: null,
            EsBorrador: true,
            ComercioExterior: comercio);

        var generador = new GeneradorDePdfCfdi();
        var tareas = Enumerable.Range(0, 6).Select(_ => Task.Run(() => generador.Generar(comprobante, datos)));
        var pdfs = await Task.WhenAll(tareas);

        Assert.All(pdfs, bytes => Assert.True(bytes.Length > 1000));
    }

    private static Comprobante FacturaBase() => new()
    {
        Id = Guid.NewGuid(),
        Estatus = "borrador",
        TipoDeComprobante = "I",
        FechaEmisionUtc = new DateTime(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc),
        LugarExpedicion = "44100",
        Moneda = "MXN",
        Exportacion = "01",
        MetodoPago = "PUE",
        FormaPago = "01",
        EmisorRfc = "EKU9003173C9",
        EmisorNombre = "ESCUELA KEMPER URGATE",
        EmisorRegimenFiscal = "601",
        ReceptorRfc = "XAXX010101000",
        ReceptorNombre = "PUBLICO EN GENERAL",
        ReceptorRegimenFiscal = "616",
        ReceptorDomicilioFiscal = "44100",
        ReceptorUsoCfdi = "S01",
        Conceptos =
        [
            new Concepto
            {
                Id = Guid.NewGuid(),
                Orden = 1,
                ClaveProdServ = "01010101",
                ClaveUnidad = "H87",
                Descripcion = "SERVICIO DE PRUEBA",
                Cantidad = 1m,
                ValorUnitario = 100m,
                Importe = 100m,
                ObjetoImp = "01"
            }
        ]
    };
}
