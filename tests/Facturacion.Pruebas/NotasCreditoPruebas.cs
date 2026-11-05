using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Egresos;
using Facturacion.Shared.Documentos;

namespace Facturacion.Pruebas;

public sealed class NotasCreditoPruebas
{
    [Fact]
    public void Bonificacion_usa_importes_positivos_y_los_impuestos_de_la_factura_original()
    {
        var origen = Origen();
        var peticion = Peticion(50);
        var resultado = CalculoDeNotaCredito.Calcular(origen, peticion, new Dictionary<int, decimal>());
        Assert.True(resultado.EsExito, resultado.Error?.Mensaje);
        var concepto = Assert.Single(resultado.Valor);
        Assert.Equal("84111506", concepto.ClaveProdServ); Assert.Equal("ACT", concepto.ClaveUnidad);
        Assert.Equal(1m, concepto.Cantidad); Assert.Equal(50m, concepto.Importe);
        Assert.Equal("02", concepto.ObjetoImp);
        Assert.Equal(8m, Assert.Single(concepto.Impuestos).Importe);
        Assert.Equal(100m, origen.Conceptos[0].Importe);
        Assert.Equal(16m, origen.Conceptos[0].Impuestos[0].Importe);
    }

    [Fact]
    public void No_supera_el_concepto_original_ni_lo_disponible_despues_de_otras_notas()
    {
        var origen = Origen();
        Assert.Equal("nota-supera-disponible", CalculoDeNotaCredito.Calcular(origen, Peticion(101), new Dictionary<int, decimal>()).Error?.Codigo);
        var usados = new Dictionary<int, decimal> { [1] = 50 };
        Assert.Equal("nota-supera-disponible", CalculoDeNotaCredito.Calcular(origen, Peticion(51), usados).Error?.Codigo);
        Assert.True(CalculoDeNotaCredito.Calcular(origen, Peticion(50), usados).EsExito);
        Assert.Equal("importe-nota-invalido", CalculoDeNotaCredito.Calcular(origen, Peticion(-1), usados).Error?.Codigo);
        Assert.Equal("importe-nota-invalido", CalculoDeNotaCredito.Calcular(origen, Peticion(.1234567m), usados).Error?.Codigo);
    }

    [Theory]
    [InlineData("OTRO", "MXN")]
    [InlineData("PUE", "USD")]
    public void No_habilita_escenarios_fuera_de_la_primera_modalidad(string metodo, string moneda)
    {
        var origen = Origen(); origen.MetodoPago = metodo; origen.Moneda = moneda;
        Assert.Equal("origen-nota-no-admitido", CalculoDeNotaCredito.ValidarOrigen(origen)?.Codigo);
    }

    [Fact]
    public void Rechaza_conceptos_repetidos_ajenos_y_relacion_no_admitida()
    {
        var origen = Origen();
        var peticion = Peticion(10);
        Assert.Equal("conceptos-nota-invalidos", CalculoDeNotaCredito.Calcular(origen,
            peticion with { Conceptos = [new(1, 10), new(1, 10)] }, new Dictionary<int, decimal>()).Error?.Codigo);
        Assert.Equal("importe-nota-invalido", CalculoDeNotaCredito.Calcular(origen,
            peticion with { Conceptos = [new(2, 10)] }, new Dictionary<int, decimal>()).Error?.Codigo);
        Assert.Equal("relacion-nota-invalida", CalculoDeNotaCredito.Calcular(origen,
            peticion with { TipoRelacion = "07" }, new Dictionary<int, decimal>()).Error?.Codigo);
    }

    [Fact]
    public void Conserva_la_diferencia_entre_iva_cero_y_exento()
    {
        var origen = Origen(); origen.Total = 100; origen.TotalImpuestosTrasladados = 0;
        var impuesto = origen.Conceptos[0].Impuestos[0]; impuesto.TasaOCuota = 0; impuesto.Importe = 0;
        var cero = CalculoDeNotaCredito.Calcular(origen, Peticion(50), new Dictionary<int, decimal>());
        Assert.True(cero.EsExito, cero.Error?.Mensaje);
        Assert.Equal(0m, Assert.Single(Assert.Single(cero.Valor).Impuestos).Importe);
        impuesto.TipoFactor = "Exento"; impuesto.TasaOCuota = null; impuesto.Importe = null;
        var exento = CalculoDeNotaCredito.Calcular(origen, Peticion(50), new Dictionary<int, decimal>());
        Assert.True(exento.EsExito, exento.Error?.Mensaje);
        Assert.Null(Assert.Single(Assert.Single(exento.Valor).Impuestos).Importe);
    }

    [Fact]
    public void Detecta_las_mezclas_que_requieren_descontar_el_desglose_de_las_notas()
    {
        var origen = Origen(); origen.MetodoPago = "PPD";
        Assert.True(CalculoDeNotaCredito.Calcular(origen, Peticion(25), new Dictionary<int, decimal>()).EsExito);
        origen.Conceptos.Add(new() { Id = Guid.NewGuid(), Orden = 2, ClaveProdServ = "81112106", ClaveUnidad = "E48",
            Descripcion = "Sin impuesto", Cantidad = 1, ValorUnitario = 100, Importe = 100, ObjetoImp = "01" });
        origen.SubTotal = 200; origen.Total = 216;
        Assert.Null(CalculoDeNotaCredito.ValidarOrigen(origen));
        Assert.True(CalculoDeNotaCredito.TieneTratamientosMixtos(origen));
    }

    [Fact]
    public void No_acredita_una_base_cruda_distinta_del_neto_publicado_en_xml()
    {
        var origen = Origen();
        var concepto = origen.Conceptos[0];
        concepto.Importe = concepto.ValorUnitario = 123.455m;
        concepto.Descuento = .005m;
        concepto.Impuestos[0].Base = 123.45m;
        concepto.Impuestos[0].Importe = 19.752m;
        origen.SubTotal = 123.46m; origen.Descuento = 0;
        origen.TotalImpuestosTrasladados = 19.75m; origen.Total = 143.21m;
        Assert.Equal("origen-nota-redondeo", CalculoDeNotaCredito.ValidarOrigen(origen)?.Codigo);
    }

    [Fact]
    public void Impide_que_las_notas_parciales_dejen_un_centavo_de_iva_pendiente()
    {
        var origen = Origen();
        var usados = new Dictionary<int, decimal> { [1] = .03m };
        // Dos bases de .03 redondean su IVA a cero, pero .06 exige .01.
        Assert.Equal("nota-redondeo-parcial", CalculoDeNotaCredito.Calcular(origen, Peticion(.03m), usados).Error?.Codigo);
        Assert.True(CalculoDeNotaCredito.Calcular(origen, Peticion(99.97m), usados).EsExito);
        Assert.Equal("importe-nota-invalido", CalculoDeNotaCredito.Calcular(origen, Peticion(.001m), usados).Error?.Codigo);
        // Tras cancelar una nota se toma el IVA real de las que siguen vigentes.
        Assert.Equal("nota-redondeo-parcial", CalculoDeNotaCredito.Calcular(origen, Peticion(99.97m), usados,
            new Dictionary<int, decimal> { [1] = .01m }).Error?.Codigo);
    }

    [Fact]
    public void No_divide_un_concepto_si_el_iva_de_la_nota_y_el_remanente_superan_el_original()
    {
        var origen = Origen(); var c = origen.Conceptos[0];
        c.Importe = c.ValorUnitario = c.Impuestos[0].Base = .08m;
        c.Impuestos[0].Importe = .0128m;
        origen.SubTotal = .08m; origen.TotalImpuestosTrasladados = .01m; origen.Total = .09m;
        Assert.Equal("nota-redondeo-parcial", CalculoDeNotaCredito.Calcular(origen, Peticion(.04m), new Dictionary<int, decimal>()).Error?.Codigo);
    }

    private static PeticionGuardarNotaCredito Peticion(decimal importe)
        => new(Guid.NewGuid(), "03", "01", null, [new(1, importe)]);
    private static Comprobante Origen() => new()
    {
        Id = Guid.NewGuid(), Uuid = Guid.NewGuid(), TipoDeComprobante = "I", Estatus = "timbrado", Variante = "basica",
        Moneda = "MXN", MetodoPago = "PUE", FormaPago = "03", Exportacion = "01", LugarExpedicion = "42000",
        EmisorRfc = "EKU9003173C9", EmisorNombre = "ESCUELA KEMPER URGATE", EmisorRegimenFiscal = "601",
        ReceptorRfc = "LCE010101AA5", ReceptorNombre = "LLANTERA DEL CENTRO", ReceptorRegimenFiscal = "601",
        ReceptorDomicilioFiscal = "42000", ReceptorUsoCfdi = "G03", SubTotal = 100, TotalImpuestosTrasladados = 16, Total = 116,
        Conceptos = [new() { Id = Guid.NewGuid(), Orden = 1, ClaveProdServ = "81112106", ClaveUnidad = "E48",
            Descripcion = "Servicio", ObjetoImp = "02", Cantidad = 1, ValorUnitario = 100, Importe = 100,
            Impuestos = [new() { Id = Guid.NewGuid(), Impuesto = "002", TipoFactor = "Tasa", TasaOCuota = .16m, Base = 100, Importe = 16 }] }]
    };
}
