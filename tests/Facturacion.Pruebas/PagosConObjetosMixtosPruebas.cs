using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Pagos;

namespace Facturacion.Pruebas;

public sealed class PagosConObjetosMixtosPruebas
{
    [Theory]
    [InlineData("01")]
    [InlineData("03")]
    [InlineData("04")]
    [InlineData("05")]
    [InlineData("06")]
    [InlineData("08")]
    public void Factura_homogenea_conserva_su_objeto_sin_inventar_impuestos(string objeto)
    {
        var resultado = DesgloseDeFacturaParaPago.Calcular(Factura(200, Concepto(objeto), Concepto(objeto)), 100);
        Assert.True(resultado.EsExito, resultado.Error?.Mensaje);
        Assert.Equal(objeto, resultado.Valor.ObjetoImpDR);
        Assert.Empty(resultado.Valor.Impuestos);
    }

    [Theory]
    [InlineData("01", "03")]
    [InlineData("03", "04")]
    [InlineData("06", "08")]
    [InlineData("01", "08")]
    public void Mezcla_sin_desglose_conserva_el_criterio_agregado_01(string primero, string segundo)
    {
        var resultado = DesgloseDeFacturaParaPago.Calcular(Factura(200, Concepto(primero), Concepto(segundo)), 100);
        Assert.True(resultado.EsExito, resultado.Error?.Mensaje);
        Assert.Equal("01", resultado.Valor.ObjetoImpDR);
        Assert.Empty(resultado.Valor.Impuestos);
    }

    [Fact]
    public void Mezcla_de_iva_cero_exento_y_sin_desglose_reparte_solo_las_bases_que_corresponden()
    {
        var factura = Factura(706, Concepto("02", Impuesto("002", .16m)),
            Concepto("02", Impuesto("002", 0)), Concepto("02", Exento()),
            Concepto("01"), Concepto("03"), Concepto("04"), Concepto("06", Impuesto("001", .10m, true)));
        var resultado = DesgloseDeFacturaParaPago.Calcular(factura, 353);
        Assert.True(resultado.EsExito, resultado.Error?.Mensaje);
        Assert.Equal("02", resultado.Valor.ObjetoImpDR);
        var impuestos = resultado.Valor.Impuestos;
        Assert.Equal(4, impuestos.Count);
        var iva = Assert.Single(impuestos, i => i.Impuesto == "002" && i.TasaOCuota == .16m);
        Assert.Equal(50m, iva.Base); Assert.Equal(8m, iva.Importe);
        var cero = Assert.Single(impuestos, i => i.Impuesto == "002" && i.TasaOCuota == 0);
        Assert.Equal(50m, cero.Base); Assert.Equal(0m, cero.Importe);
        var exento = Assert.Single(impuestos, i => i.TipoFactor == "Exento");
        Assert.Equal(50m, exento.Base); Assert.Null(exento.Importe); Assert.Null(exento.TasaOCuota);
        var isr = Assert.Single(impuestos, i => i.Impuesto == "001");
        Assert.True(isr.EsRetencion); Assert.Equal(50m, isr.Base); Assert.Equal(5m, isr.Importe);
    }

    [Fact]
    public void Mezcla_07_06_y_01_conserva_ieps_e_isr_y_no_crea_iva()
    {
        var resultado = DesgloseDeFacturaParaPago.Calcular(Factura(307,
            Concepto("07", Impuesto("003", .08m)), Concepto("06", Impuesto("001", .01m, true)), Concepto("01")), 153.50m);
        Assert.True(resultado.EsExito, resultado.Error?.Mensaje);
        Assert.Equal("02", resultado.Valor.ObjetoImpDR);
        Assert.Equal(4m, Assert.Single(resultado.Valor.Impuestos, i => i.Impuesto == "003").Importe);
        Assert.Equal(.5m, Assert.Single(resultado.Valor.Impuestos, i => i.Impuesto == "001").Importe);
        Assert.DoesNotContain(resultado.Valor.Impuestos, i => i.Impuesto == "002");
    }

    [Fact]
    public void Un_concepto_totalmente_descontado_no_impide_pagar_los_demas()
    {
        var descontado = Concepto("02", Impuesto("002", .16m));
        descontado.Descuento = 100;
        descontado.Impuestos[0].Base = 0; descontado.Impuestos[0].Importe = 0;
        var resultado = DesgloseDeFacturaParaPago.Calcular(Factura(216, descontado,
            Concepto("02", Impuesto("002", .16m)), Concepto("01")), 108);
        Assert.True(resultado.EsExito, resultado.Error?.Mensaje);
        var iva = Assert.Single(resultado.Valor.Impuestos);
        Assert.Equal(50m, iva.Base); Assert.Equal(8m, iva.Importe);
    }

    [Fact]
    public void Agrupar_no_oculta_un_concepto_invalido_ni_la_mezcla_prohibida_05()
    {
        var incompatible = Factura(232, Concepto("02", Impuesto("002", .16m)), Concepto("06", Impuesto("002", .16m)));
        Assert.Equal("impuestos-factura-incompatibles", DesgloseDeFacturaParaPago.Calcular(incompatible, 116).Error?.Codigo);
        var mezcla = Factura(216, Concepto("02", Impuesto("002", .16m)), Concepto("05"));
        Assert.Equal("objetos-factura-incompatibles", DesgloseDeFacturaParaPago.Calcular(mezcla, 108).Error?.Codigo);
        var incompleto = Factura(100, Concepto("02"));
        Assert.Equal("impuestos-factura-incompatibles", DesgloseDeFacturaParaPago.Calcular(incompleto, 50).Error?.Codigo);
    }

    [Fact]
    public void Detecta_desglose_guardado_distinto_aunque_su_base_por_tasa_cuadre()
    {
        var resultado = DesgloseDeFacturaParaPago.Calcular(Factura(216,
            Concepto("02", Impuesto("002", .16m)), Concepto("01")), 108);
        Assert.True(resultado.EsExito, resultado.Error?.Mensaje);
        var documento = new DocumentoPagado
        {
            ObjetoImpDR = "02", MonedaDR = "MXN",
            Impuestos = [new() { Impuesto = "002", TipoFactor = "Tasa", TasaOCuota = .16m, Base = 50, Importe = 8 }]
        };
        Assert.True(resultado.Valor.CoincideCon(documento));
        documento.Impuestos[0].Base = 100; documento.Impuestos[0].Importe = 16;
        Assert.False(resultado.Valor.CoincideCon(documento));
        documento.Impuestos.Clear(); documento.ObjetoImpDR = "01";
        Assert.False(resultado.Valor.CoincideCon(documento));
    }

    private static Concepto Concepto(string objeto, params ImpuestoConcepto[] impuestos)
        => new() { ClaveProdServ = "81112106", ClaveUnidad = "E48", Descripcion = "Servicio",
            Cantidad = 1, ValorUnitario = 100, Importe = 100, ObjetoImp = objeto, Impuestos = [.. impuestos] };
    private static ImpuestoConcepto Impuesto(string clave, decimal tasa, bool retencion = false)
        => new() { Impuesto = clave, TipoFactor = "Tasa", TasaOCuota = tasa, Base = 100, Importe = 100 * tasa, EsRetencion = retencion };
    private static ImpuestoConcepto Exento()
        => new() { Impuesto = "002", TipoFactor = "Exento", Base = 100 };
    private static Comprobante Factura(decimal total, params Concepto[] conceptos)
        => new()
        {
            Estatus = "timbrado", TipoDeComprobante = "I", LugarExpedicion = "42000", Moneda = "MXN", Exportacion = "01",
            EmisorRfc = "EKU9003173C9", EmisorNombre = "ESCUELA KEMPER URGATE", EmisorRegimenFiscal = "601",
            ReceptorRfc = "LCE010101AA5", ReceptorNombre = "LLANTERA DEL CENTRO", ReceptorRegimenFiscal = "601",
            ReceptorDomicilioFiscal = "42000", ReceptorUsoCfdi = "G03", Total = total, Conceptos = [.. conceptos]
        };
}
