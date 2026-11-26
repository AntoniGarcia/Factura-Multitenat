using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.Pdf;
using Facturacion.Server.Modules.Documentos.Obras;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.Documentos;
using Facturacion.Shared.Obras;
using QuestPDF.Infrastructure;

namespace Facturacion.Pruebas;

public sealed class ObraYImporteEnLetraPruebas
{
    [Theory]
    [InlineData(341400.00, "MXN", "TRESCIENTOS CUARENTA Y UN MIL CUATROCIENTOS PESOS 00/100 M.N.")]
    [InlineData(1.00, "MXN", "UN PESO 00/100 M.N.")]
    [InlineData(1000000.50, "MXN", "UN MILLÓN DE PESOS 50/100 M.N.")]
    [InlineData(0.00, "MXN", "CERO PESOS 00/100 M.N.")]
    [InlineData(125.75, "USD", "CIENTO VEINTICINCO DÓLARES 75/100 USD")]
    public void Importe_en_letra_convierte_correctamente(decimal monto, string moneda, string esperado)
    {
        Assert.Equal(esperado, ImporteEnLetra.Convertir(monto, moneda));
    }

    [Fact]
    public void Calculo_de_obra_reproduce_el_caso_de_referencia_del_ingeniero()
    {
        var comprobante = ComprobanteDeObra(300_000m);
        var datos = new DatosObra
        {
            Id = Guid.NewGuid(),
            ComprobanteId = comprobante.Id,
            TipoObra = null,
            PorcentajeAmortizacion = 0m,
            PorcentajeRetenciones = 0m,
            Retenciones = 0m,
            PorcentajeDevoluciones = 0m,
            Devoluciones = 0m,
            NombreDeduccion1 = "5 al millar",
            PorcentajeDeduccion1 = 0.50m,
            NombreDeduccion2 = "1% OBS",
            PorcentajeDeduccion2 = 1.00m,
            NombreDeduccion3 = "ICIC",
            PorcentajeDeduccion3 = 0.20m,
            NombreDeduccion4 = "ÚNETE",
            PorcentajeDeduccion4 = 0.50m
        };

        var calculado = ServicioDeObras.Calcular(comprobante, datos);

        Assert.Equal(300_000m, calculado.ImporteTrabajos);
        Assert.Equal(0m, calculado.Amortizacion);
        Assert.Equal(300_000m, calculado.SubtotalEstimacion);
        Assert.Equal(48_000m, calculado.IvaEstimado);
        Assert.Equal(348_000m, calculado.TotalEstimacion);
        Assert.Equal(6_600m, calculado.Deducciones.Sum(d => d.Importe));
        Assert.Equal(341_400m, calculado.ImporteLiquido);
        Assert.Equal("TRESCIENTOS CUARENTA Y UN MIL CUATROCIENTOS PESOS 00/100 M.N.",
            ImporteEnLetra.Convertir(calculado.ImporteLiquido, "MXN"));
    }

    [Fact]
    public void Calculo_de_obra_resta_amortizacion_y_retenciones_suma_devoluciones_y_saca_iva_del_subtotal()
    {
        var comprobante = ComprobanteDeObra(300_000m);
        var datos = new DatosObra
        {
            Id = Guid.NewGuid(),
            ComprobanteId = comprobante.Id,
            TipoObra = null,
            PorcentajeAmortizacion = 10m,
            PorcentajeRetenciones = 5m,
            Retenciones = 15_000m,
            PorcentajeDevoluciones = 2m,
            Devoluciones = 6_000m,
            NombreDeduccion1 = "5 al millar",
            PorcentajeDeduccion1 = 0.50m,
            NombreDeduccion2 = "1% OBS",
            PorcentajeDeduccion2 = 1.00m,
            NombreDeduccion3 = "ICIC",
            PorcentajeDeduccion3 = 0.20m,
            NombreDeduccion4 = "ÚNETE",
            PorcentajeDeduccion4 = 0.50m
        };

        // Aplicar amortización sin exigir CFDI relacionado tipo 07
        var errorAplicar = AmortizacionDeObra.Aplicar(comprobante, datos, decimalesMoneda: 2);
        Assert.Null(errorAplicar);
        Assert.Null(ValidadorFiscalDeObra.Validar(comprobante, datos));

        var calculado = ServicioDeObras.Calcular(comprobante, datos);

        // 300,000 - 30,000 (amort) - 15,000 (ret) + 6,000 (dev) = 261,000
        Assert.Equal(30_000m, calculado.Amortizacion);
        Assert.Equal(261_000m, calculado.SubtotalEstimacion);
        // IVA 16% sobre subtotal 261,000 = 41,760
        Assert.Equal(41_760m, calculado.IvaEstimado);
        // Total = 261,000 + 41,760 = 302,760
        Assert.Equal(302_760m, calculado.TotalEstimacion);
        // Importe líquido = 302,760 - 6,600 = 296,160
        Assert.Equal(296_160m, calculado.ImporteLiquido);

        // Genera PDF de obra sin errores
        QuestPDF.Settings.License = LicenseType.Community;
        var pdf = new GeneradorDePdfCfdi().Generar(comprobante, new DatosDelPdf(
            new DateTime(2026, 11, 25, 12, 0, 0), 2, null, EsBorrador: true) { EstimacionObra = calculado });
        Assert.True(pdf.Length > 1000);
    }

    [Fact]
    public void Calculo_de_obra_conserva_seis_decimales_y_funciona_sin_tipo_de_obra()
    {
        var comprobante = ComprobanteDeObra(100.123456m);
        var datos = new DatosObra
        {
            Id = Guid.NewGuid(),
            ComprobanteId = comprobante.Id,
            TipoObra = null,
            PorcentajeAmortizacion = 12.5m,
            PorcentajeRetenciones = 2.25m,
            Retenciones = Math.Round(100.123456m * 2.25m / 100m, 6, MidpointRounding.ToEven),
            PorcentajeDevoluciones = 1.125m,
            Devoluciones = Math.Round(100.123456m * 1.125m / 100m, 6, MidpointRounding.ToEven),
            NombreDeduccion1 = "5 al millar",
            PorcentajeDeduccion1 = 0.50m,
            NombreDeduccion2 = "1% OBS",
            PorcentajeDeduccion2 = 0m,
            NombreDeduccion3 = "ICIC",
            PorcentajeDeduccion3 = 0m,
            NombreDeduccion4 = "ÚNETE",
            PorcentajeDeduccion4 = 0m
        };

        var errorAplicar = AmortizacionDeObra.Aplicar(comprobante, datos, decimalesMoneda: 2);
        Assert.Null(errorAplicar);
        Assert.Null(ValidadorFiscalDeObra.Validar(comprobante, datos));

        var calculado = ServicioDeObras.Calcular(comprobante, datos);
        // Sin redondeo prematuro de IVA a 2 decimales
        var esperadoIva = Math.Round(calculado.SubtotalEstimacion * 0.16m, 6, MidpointRounding.ToEven);
        Assert.Equal(esperadoIva, calculado.IvaEstimado);
        Assert.Equal(calculado.SubtotalEstimacion + esperadoIva, calculado.TotalEstimacion);
        Assert.NotNull(RetencionLocalDeObra.Calcular(datos, comprobante.SubTotal));
    }

    private static Comprobante ComprobanteDeObra(decimal importe)
    {
        var id = Guid.NewGuid();
        var iva = Math.Round(importe * 0.16m, 2);
        return new Comprobante
        {
            Id = id,
            Estatus = "borrador",
            TipoDeComprobante = "I",
            Variante = VariantesDeFactura.Obra,
            FechaEmisionUtc = new DateTime(2026, 11, 25, 18, 0, 0, DateTimeKind.Utc),
            LugarExpedicion = "44100",
            Moneda = "MXN",
            Exportacion = "01",
            MetodoPago = "PUE",
            FormaPago = "03",
            EmisorRfc = "EKU9003173C9",
            EmisorNombre = "CONSTRUCTORA DE PRUEBA",
            EmisorRegimenFiscal = "601",
            ReceptorRfc = "AAA010101AAA",
            ReceptorNombre = "DEPENDENCIA PUBLICA",
            ReceptorRegimenFiscal = "603",
            ReceptorDomicilioFiscal = "44100",
            ReceptorUsoCfdi = "G03",
            SubTotal = importe,
            Descuento = 0m,
            TotalImpuestosTrasladados = iva,
            Total = importe + iva,
            Conceptos =
            [
                new Concepto
                {
                    Id = Guid.NewGuid(),
                    ComprobanteId = id,
                    Orden = 1,
                    ClaveProdServ = "70121500",
                    ClaveUnidad = "LTR",
                    Descripcion = "ARRENDAMIENTO OCTUBRE",
                    Cantidad = 1m,
                    ValorUnitario = importe,
                    Importe = importe,
                    ObjetoImp = "02",
                    Impuestos =
                    [
                        new ImpuestoConcepto
                        {
                            Id = Guid.NewGuid(),
                            Impuesto = "002",
                            TipoFactor = "Tasa",
                            TasaOCuota = 0.160000m,
                            Base = importe,
                            Importe = iva,
                            EsRetencion = false
                        }
                    ]
                }
            ]
        };
    }
}
