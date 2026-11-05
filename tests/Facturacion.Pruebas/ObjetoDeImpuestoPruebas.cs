using System.Text;
using System.Xml.Linq;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Impuestos;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Server.Modules.Plataforma.Productos;

namespace Facturacion.Pruebas;

public sealed class ObjetoDeImpuestoPruebas
{
    [Theory]
    [InlineData("01")]
    [InlineData("03")]
    [InlineData("04")]
    [InlineData("05")]
    [InlineData("06")]
    [InlineData("08")]
    public void Sin_desglose_no_inventa_impuestos_y_admite_csv_sin_iva(string objeto)
    {
        var calculo = MotorDeImpuestos.Calcular(new("MXN", null,
            [new(1, 100, 0, objeto, [])]));
        Assert.True(calculo.EsExito, calculo.Error?.Mensaje);
        Assert.Equal(100m, calculo.Valor.Total);
        Assert.Empty(calculo.Valor.Traslados);
        Assert.Empty(calculo.Valor.Retenciones);
        Assert.Null(GeneradorDeXmlPago.Validar(Pago(objeto), 2));

        using var csv = new MemoryStream(Encoding.UTF8.GetBytes($"01010101,E48,Servicio,Prueba,100,,{objeto},"));
        var importacion = LectorDeCsvDeProductos.Analizar(csv);
        Assert.Null(Assert.Single(importacion.Renglones).Error);
    }

    [Theory]
    [InlineData("06")]
    [InlineData("08")]
    public void Sin_traslado_admite_retencion_isr_y_la_conserva_en_el_pago(string objeto)
    {
        var calculo = MotorDeImpuestos.Calcular(new("MXN", null,
            [new(1, 100, 0, objeto, [new("001", "Tasa", 0.0125m, true)])]));
        Assert.True(calculo.EsExito, calculo.Error?.Mensaje);
        Assert.Equal(98.75m, calculo.Valor.Total);

        var pago = Pago(objeto, new ImpuestoDocumentoPagado
        {
            Impuesto = "001", TipoFactor = "Tasa", TasaOCuota = 0.0125m,
            Base = 100, Importe = 1.25m, EsRetencion = true
        });
        Assert.Null(GeneradorDeXmlPago.Validar(pago, 2));
        XNamespace p = "http://www.sat.gob.mx/Pagos20";
        var xml = GeneradorDeXmlPago.Generar(pago, new(DateTime.Now, 2, "30001000000400002434", "TUVCNjQ=") { ZonaHoraria = TimeZoneInfo.Utc });
        Assert.Equal(objeto, (string?)xml.Descendants(p + "DoctoRelacionado").Single().Attribute("ObjetoImpDR"));
        Assert.Single(xml.Descendants(p + "RetencionDR"));
        Assert.Empty(xml.Descendants(p + "TrasladoDR"));
    }

    [Fact]
    public void Objeto_07_calcula_ieps_e_isr_sin_iva()
    {
        var calculo = MotorDeImpuestos.Calcular(new("MXN", null,
            [new(1, 100, 0, "07", [new("003", "Tasa", 0.08m, false), new("001", "Tasa", 0.0125m, true)])]));
        Assert.True(calculo.EsExito, calculo.Error?.Mensaje);
        Assert.Equal(106.75m, calculo.Valor.Total);
        var pago = Pago("07", new ImpuestoDocumentoPagado
        {
            Impuesto = "003", TipoFactor = "Tasa", TasaOCuota = 0.08m,
            Base = 100, Importe = 8, EsRetencion = false
        });
        Assert.Null(GeneradorDeXmlPago.Validar(pago, 2));
    }

    [Theory]
    [InlineData("01")]
    [InlineData("02")]
    [InlineData("04")]
    [InlineData("08")]
    public void Credito_podebi_no_se_mezcla_con_otros_objetos(string otro)
    {
        var calculo = MotorDeImpuestos.Calcular(new("MXN", null,
            [new(1, 100, 0, "05", []), new(1, 100, 0, otro, [])]));
        Assert.Equal("objeto-05-no-combinable", calculo.Error?.Codigo);
    }

    [Theory]
    [InlineData("03", "002", false)]
    [InlineData("04", "002", false)]
    [InlineData("05", "002", false)]
    [InlineData("06", "002", false)]
    [InlineData("06", "003", false)]
    [InlineData("08", "003", false)]
    [InlineData("08", "001", false)]
    [InlineData("07", "002", false)]
    [InlineData("07", "001", true)]
    [InlineData("09", "002", false)]
    public void Rechaza_desglose_incompatible_en_factura_y_pago(string objeto, string impuesto, bool retencion)
    {
        Assert.True(MotorDeImpuestos.Calcular(new("MXN", null,
            [new(1, 100, 0, objeto, [new(impuesto, "Tasa", 0.08m, retencion)])])).EsFallo);
        Assert.NotNull(GeneradorDeXmlPago.Validar(Pago(objeto, new ImpuestoDocumentoPagado
        {
            Impuesto = impuesto, TipoFactor = "Tasa", TasaOCuota = 0.08m,
            Base = 100, Importe = 8, EsRetencion = retencion
        }), 2));
    }

    private static Comprobante Pago(string objeto, params ImpuestoDocumentoPagado[] impuestos)
    {
        var comprobante = ComprobanteDeEjemplo.CrearPago();
        var documento = comprobante.Pagos.Single().Documentos.Single();
        documento.ObjetoImpDR = objeto;
        documento.Impuestos.Clear();
        documento.Impuestos.AddRange(impuestos);
        return comprobante;
    }
}
