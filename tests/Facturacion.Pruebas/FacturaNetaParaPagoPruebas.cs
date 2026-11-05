using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Egresos;
using Facturacion.Server.Modules.Documentos.Pagos;
using Facturacion.Shared.Documentos;

namespace Facturacion.Pruebas;

public sealed class FacturaNetaParaPagoPruebas
{
    [Fact]
    public void Descuenta_cada_tratamiento_y_reparte_solo_los_impuestos_restantes()
    {
        var origen = Origen(); var nota = Nota(origen, [new(1, 50), new(2, 20), new(3, 30), new(4, 10)], 110, 8);
        var neta = FacturaNetaParaPago.Calcular(origen, [nota]);
        Assert.True(neta.EsExito, neta.Error?.Mensaje); Assert.Equal(298m, neta.Valor.Total);
        var pago = DesgloseDeFacturaParaPago.Calcular(neta.Valor, 149);
        Assert.True(pago.EsExito, pago.Error?.Mensaje); Assert.Equal("02", pago.Valor.ObjetoImpDR);
        Assert.Equal(25m, pago.Valor.Impuestos.Single(i => i.TasaOCuota == .16m).Base);
        Assert.Equal(4m, pago.Valor.Impuestos.Single(i => i.TasaOCuota == .16m).Importe);
        Assert.Equal(40m, pago.Valor.Impuestos.Single(i => i.TasaOCuota == 0).Base);
        Assert.Equal(35m, pago.Valor.Impuestos.Single(i => i.TipoFactor == "Exento").Base);
        Assert.Equal(416m, origen.Total); Assert.All(origen.Conceptos, c => Assert.Equal(100m, c.Importe));
    }

    [Fact]
    public void Omite_los_conceptos_acreditados_por_completo_y_reclasifica_lo_que_se_paga()
    {
        var origen = Origen(); var nota = Nota(origen, [new(1, 100), new(2, 100), new(3, 100)], 300, 16);
        var neta = FacturaNetaParaPago.Calcular(origen, [nota]);
        Assert.True(neta.EsExito, neta.Error?.Mensaje); Assert.Single(neta.Valor.Conceptos);
        var pago = DesgloseDeFacturaParaPago.Calcular(neta.Valor, 100);
        Assert.True(pago.EsExito); Assert.Equal("01", pago.Valor.ObjetoImpDR); Assert.Empty(pago.Valor.Impuestos);
    }

    [Fact]
    public void Rechaza_notas_ajenas_conceptos_inexistentes_y_desglose_alterado()
    {
        var origen = Origen(); var nota = Nota(origen, [new(1, 50)], 50, 8);
        nota.Relacionados[0].UuidRelacionado = Guid.NewGuid();
        Assert.True(FacturaNetaParaPago.Calcular(origen, [nota]).EsFallo);
        nota.Relacionados[0].UuidRelacionado = origen.Uuid!.Value;
        nota.Conceptos[0].Orden = 20; Assert.True(FacturaNetaParaPago.Calcular(origen, [nota]).EsFallo);
        nota.Conceptos[0].Orden = 1; nota.Conceptos[0].Impuestos[0].Base = 100;
        Assert.True(FacturaNetaParaPago.Calcular(origen, [nota]).EsFallo);
    }

    [Fact]
    public void Nota_posterior_descuenta_las_bases_ya_cobradas_antes_de_repartir_otro_pago()
    {
        var origen = Origen(); var nota = Nota(origen, [new(1, 25)], 25, 4);
        var neta = FacturaNetaParaPago.Calcular(origen, [nota]); Assert.True(neta.EsExito);
        var saldo = SaldoFiscalParaPago.Calcular(neta.Valor, [PagoDeLaMitad()]);
        Assert.True(saldo.EsExito, saldo.Error?.Mensaje); Assert.Equal(179m, saldo.Valor.Total);
        var siguiente = saldo.Valor.CalcularAbono(89.5m); Assert.True(siguiente.EsExito);
        Assert.Equal(12.5m, siguiente.Valor.Impuestos.Single(i => i.TasaOCuota == .16m).Base);
        Assert.Equal(2m, siguiente.Valor.Impuestos.Single(i => i.TasaOCuota == .16m).Importe);
        Assert.Equal(25m, siguiente.Valor.Impuestos.Single(i => i.TasaOCuota == 0).Base);
        Assert.Equal(25m, siguiente.Valor.Impuestos.Single(i => i.TipoFactor == "Exento").Base);
    }

    [Fact]
    public void No_acredita_bases_ya_pagadas_aunque_quede_saldo_en_otros_grupos()
    {
        var origen = Origen(); var nota = Nota(origen, [new(1, 60)], 60, 9.6m);
        var neta = FacturaNetaParaPago.Calcular(origen, [nota]); Assert.True(neta.EsExito);
        Assert.Equal("saldo-fiscal-no-disponible", SaldoFiscalParaPago.Calcular(neta.Valor, [PagoDeLaMitad()]).Error?.Codigo);
    }

    [Fact]
    public void Puede_acreditar_todo_el_remanente_y_no_reutiliza_impuestos_cobrados()
    {
        var origen = Origen(); var nota = Nota(origen, [new(1, 50), new(2, 50), new(3, 50), new(4, 50)], 200, 8);
        var neta = FacturaNetaParaPago.Calcular(origen, [nota]); Assert.True(neta.EsExito);
        var saldo = SaldoFiscalParaPago.Calcular(neta.Valor, [PagoDeLaMitad()]);
        Assert.True(saldo.EsExito, saldo.Error?.Mensaje); Assert.Equal(0m, saldo.Valor.Total); Assert.Empty(saldo.Valor.Impuestos);
        Assert.True(saldo.Valor.CalcularAbono(1).EsFallo);
    }

    [Fact]
    public void Rechaza_el_historial_de_pago_con_iva_que_no_coincide_con_su_base()
    {
        var origen = Origen(); var pago = PagoDeLaMitad(); pago.Impuestos[0].Importe = 9;
        Assert.True(SaldoFiscalParaPago.Calcular(origen, [pago]).EsFallo);
    }

    private static DocumentoPagado PagoDeLaMitad() => new()
    {
        MonedaDR = "MXN", ObjetoImpDR = "02", ImpSaldoAnt = 416, ImpPagado = 208, ImpSaldoInsoluto = 208,
        Impuestos = [new() { Impuesto = "002", TipoFactor = "Tasa", TasaOCuota = .16m, Base = 50, Importe = 8 },
            new() { Impuesto = "002", TipoFactor = "Tasa", TasaOCuota = 0, Base = 50, Importe = 0 },
            new() { Impuesto = "002", TipoFactor = "Exento", Base = 50 }]
    };

    private static Comprobante Nota(Comprobante origen, IReadOnlyList<RenglonNotaCreditoDto> lineas, decimal subtotal, decimal iva)
    {
        var calculado = CalculoDeNotaCredito.Calcular(origen, new(null, "03", "01", null, lineas), new Dictionary<int, decimal>());
        Assert.True(calculado.EsExito, calculado.Error?.Mensaje);
        var nota = Cabecera(); nota.TipoDeComprobante = "E"; nota.Variante = CalculoDeNotaCredito.Variante;
        nota.MetodoPago = "PUE"; nota.ReceptorUsoCfdi = "G02";
        nota.Conceptos.AddRange(calculado.Valor); nota.SubTotal = subtotal; nota.TotalImpuestosTrasladados = iva; nota.Total = subtotal + iva;
        nota.Relacionados.Add(new() { TipoRelacion = "01", UuidRelacionado = origen.Uuid!.Value }); return nota;
    }
    private static Comprobante Origen()
    {
        var c = Cabecera(); c.Uuid = Guid.NewGuid(); c.SubTotal = 400; c.TotalImpuestosTrasladados = 16; c.Total = 416;
        for (var orden = 1; orden <= 4; orden++)
        {
            var concepto = new Concepto { Orden = orden, ClaveProdServ = "81112106", ClaveUnidad = "E48", Descripcion = "Servicio",
                Cantidad = 1, ValorUnitario = 100, Importe = 100, ObjetoImp = orden == 4 ? "01" : "02" };
            if (orden < 4) concepto.Impuestos.Add(new() { Impuesto = "002", TipoFactor = orden == 3 ? "Exento" : "Tasa",
                Base = 100, TasaOCuota = orden == 3 ? null : orden == 1 ? .16m : 0m, Importe = orden == 3 ? null : orden == 1 ? 16m : 0m });
            c.Conceptos.Add(concepto);
        }
        return c;
    }
    private static Comprobante Cabecera() => new()
    {
        Estatus = "timbrado", TipoDeComprobante = "I", MetodoPago = "PPD", Moneda = "MXN", Exportacion = "01", LugarExpedicion = "42000",
        EmisorRfc = "EKU9003173C9", EmisorNombre = "ESCUELA KEMPER URGATE", EmisorRegimenFiscal = "601",
        ReceptorRfc = "LCE010101AA5", ReceptorNombre = "LLANTERA DEL CENTRO", ReceptorRegimenFiscal = "601",
        ReceptorDomicilioFiscal = "42000", ReceptorUsoCfdi = "G03"
    };
}
