using System.Xml.Linq;
using System.Xml.Schema;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.Documentos;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Facturacion.Pruebas;

public sealed class EquivalenciasDePagoPruebas
{
    [Fact]
    public void La_equivalencia_tiene_sentido_y_precision_de_factor()
    {
        Assert.True(ConversionDePago.EsEquivalenciaValida("MXN", "MXN", null));
        Assert.False(ConversionDePago.EsEquivalenciaValida("MXN", "MXN", 20));
        Assert.False(ConversionDePago.EsEquivalenciaValida("USD", "MXN", null));
        Assert.True(ConversionDePago.EsEquivalenciaValida("USD", "MXN", 0.05m));
        Assert.False(ConversionDePago.EsEquivalenciaValida("USD", "MXN", 0.00000000001m));
        Assert.Equal(2000m, ConversionDePago.AMonedaDelPago(100m, 0.05m));
        Assert.True(ConversionDePago.Cuadra(2000, 2, [(100m, 0.05m, 2, false)]));
        Assert.False(ConversionDePago.Cuadra(100, 2, [(100m, 0.05m, 2, false)]));
        Assert.True(ConversionDePago.Cuadra(3000, 2, [(100m, 0.05m, 2, false), (1000m, 1m, 2, true)]));
        Assert.False(ConversionDePago.Cuadra(3001, 2, [(100m, 0.05m, 2, false), (1000m, 1m, 2, true)]));
    }

    [Fact]
    public void Conversiones_con_diez_decimales_y_margenes_no_ocultan_diferencias_reales()
    {
        Assert.True(ConversionDePago.Cuadra(100, 2, [(104.66m, 1.0465711361m, 2, false)]));
        Assert.False(ConversionDePago.Cuadra(99, 2, [(104.66m, 1.0465711361m, 2, false)]));
        Assert.True(ConversionDePago.Cuadra(100, 0, [(15000m, 150m, 0, false)]));
        Assert.False(ConversionDePago.Cuadra(1, 2, [(999999999999m, 0.0000000001m, 2, false)]));
        Assert.True(ConversionDePago.Cuadra(33.33m, 2, [(100m, 3m, 2, false)]));
        Assert.True(ConversionDePago.Cuadra(33.34m, 2, [(100m, 3m, 2, false)]));
        Assert.False(ConversionDePago.Cuadra(33.32m, 2, [(100m, 3m, 2, false)]));
    }

    [HechoConEsquemasSat]
    public void Xml_con_factura_en_otra_moneda_conserva_sus_decimales_y_convierte_impuestos()
    {
        var c = ComprobanteDeEjemplo.CrearPago();
        var pago = c.Pagos.Single();
        var d = pago.Documentos.Single();
        pago.MonedaP = "MXN"; pago.TipoCambioP = null; pago.Monto = 2000;
        pago.RfcEmisorCtaOrd = null; pago.RfcEmisorCtaBen = null;
        pago.CtaOrdenante = null; pago.CtaBeneficiario = null; pago.NomBancoOrdExt = null;
        d.MonedaDR = "USD"; d.DecimalesMonedaDR = 2; d.EquivalenciaDR = 0.05m;
        d.ImpSaldoAnt = 100; d.ImpPagado = 100; d.ImpSaldoInsoluto = 0; d.ObjetoImpDR = "02";
        d.Impuestos.Clear(); d.Impuestos.Add(new() { Impuesto = "002", TipoFactor = "Tasa", TasaOCuota = 0.16m, Base = 86.206897m, Importe = 13.793104m });
        // Los importes DR siguen en USD; el desglose P se divide entre la equivalencia.
        Assert.Null(GeneradorDeXmlPago.Validar(c, 2));
        var xml = GeneradorDeXmlPago.Generar(c, new(DateTime.Now, 2, "30001000000400002434", "TUVCNjQ=") { ZonaHoraria = TimeZoneInfo.Utc });
        XNamespace p = "http://www.sat.gob.mx/Pagos20";
        Assert.Equal("0.0500000000", (string?)xml.Descendants(p + "DoctoRelacionado").Single().Attribute("EquivalenciaDR"));
        Assert.Equal("100.00", (string?)xml.Descendants(p + "DoctoRelacionado").Single().Attribute("ImpPagado"));
        Assert.Equal("275.862080", (string?)xml.Descendants(p + "TrasladoP").Single().Attribute("ImporteP"));
        xml.Root!.SetAttributeValue("Sello", "AA==");
        var esquemas = new EsquemasSat(new Entorno(), Options.Create(new OpcionesDeEsquemasSat { Ruta = "CatalogosSAT" }));
        var errores = new List<string>(); xml.Validate(esquemas.EsquemaCfdiPagos, (_, e) => errores.Add(e.Message)); Assert.Empty(errores);
        d.MonedaDR = "JPY"; d.DecimalesMonedaDR = 0; d.EquivalenciaDR = 150; pago.MonedaP = "USD"; pago.TipoCambioP = 20;
        pago.Monto = 100; d.ImpSaldoAnt = 15000; d.ImpPagado = 15000; d.ObjetoImpDR = "01"; d.Impuestos.Clear();
        xml = GeneradorDeXmlPago.Generar(c, new(DateTime.Now, 2, "30001000000400002434", "TUVCNjQ=") { ZonaHoraria = TimeZoneInfo.Utc });
        Assert.Equal("15000", (string?)xml.Descendants(p + "DoctoRelacionado").Single().Attribute("ImpPagado"));
    }

    private sealed class Entorno : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Pruebas";
        public string ContentRootPath { get; set; } = Path.GetFullPath(Path.Combine(EsquemasSatPruebas.Raiz, ".."));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
