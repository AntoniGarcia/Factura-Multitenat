using System.Xml.Linq;
using System.Xml.Schema;
using Facturacion.Server.Modules.Documentos.Pagos;
using Facturacion.Server.Modules.Documentos.Salidas;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Facturacion.Pruebas;

public sealed class CadenaDePagoPruebas
{
    [Fact]
    public void Spei_es_opcional_pero_su_grupo_debe_estar_completo_y_solo_aplica_a_transferencias()
    {
        Assert.Null(ReglasDeCadenaPago.Validar("03", null, null, null, null));
        Assert.Null(ReglasDeCadenaPago.Validar("01", null, null, null, null));
        Assert.Null(ReglasDeCadenaPago.Validar("03", "01", "AA==", "||Banco|100||", "AQ=="));
        Assert.Equal("cadena-pago-no-aplica", ReglasDeCadenaPago.Validar("01", "01", "AA==", "cadena", "AQ==")?.Codigo);
        Assert.Equal("tipo-cadena-pago-invalido", ReglasDeCadenaPago.Validar("03", null, "AA==", "cadena", "AQ==")?.Codigo);
        Assert.Equal("tipo-cadena-pago-invalido", ReglasDeCadenaPago.Validar("03", "02", "AA==", "cadena", "AQ==")?.Codigo);
        Assert.Equal("cadena-pago-incompleta", ReglasDeCadenaPago.Validar("03", "01", null, "cadena", "AQ==")?.Codigo);
        Assert.Equal("cadena-pago-incompleta", ReglasDeCadenaPago.Validar("03", "01", "AA==", null, "AQ==")?.Codigo);
        Assert.Equal("cadena-pago-incompleta", ReglasDeCadenaPago.Validar("03", "01", "AA==", "cadena", null)?.Codigo);
    }

    [Fact]
    public void Rechaza_base64_invalido_longitudes_y_caracteres_que_no_admite_xml()
    {
        Assert.Equal("cadena-pago-base64-invalida", ReglasDeCadenaPago.Validar("03", "01", "***", "cadena", "AQ==")?.Codigo);
        Assert.Equal("cadena-pago-base64-invalida", ReglasDeCadenaPago.Validar("03", "01", "AA==", "cadena", "%%%%")?.Codigo);
        Assert.Equal("cadena-pago-caracteres-invalidos", ReglasDeCadenaPago.Validar("03", "01", "AA==", "cadena\0", "AQ==")?.Codigo);
        Assert.Equal("cadena-pago-demasiado-larga", ReglasDeCadenaPago.Validar("03", "01", "AA==", new string('x', 8193), "AQ==")?.Codigo);
        Assert.Equal("cadena-pago-demasiado-larga", ReglasDeCadenaPago.Validar("03", "01", new string('A', 16388), "cadena", "AQ==")?.Codigo);
        Assert.Equal("cadena-pago-demasiado-larga", ReglasDeCadenaPago.Validar("03", "01", "AA==", "cadena", new string('A', 4100))?.Codigo);
        Assert.Null(ReglasDeCadenaPago.Validar("03", "01", " AA==\n", "||Banco & receptor||", "AQ=="));
    }

    [HechoConEsquemasSat]
    public void Xml_conserva_los_cuatro_datos_spei_y_omite_el_grupo_cuando_no_se_captura()
    {
        var c = ComprobanteDeEjemplo.CrearPago();
        var p = c.Pagos[0];
        p.Documentos[0].Impuestos.Clear(); p.Documentos[0].ObjetoImpDR = "01";
        p.RfcEmisorCtaOrd = null; p.RfcEmisorCtaBen = null;
        p.CtaOrdenante = null; p.CtaBeneficiario = null; p.NomBancoOrdExt = null;
        p.TipoCadPago = "01"; p.CertPago = "AA==";
        p.CadPago = "||Banco & receptor|\"Referencia\"|32160.00||"; p.SelloPago = "AQ==";
        var datos = new DatosDeEmision(DateTime.Now, 2, "30001000000400002434", "TUVCNjQ=") { ZonaHoraria = TimeZoneInfo.Utc };
        var xml = GeneradorDeXmlPago.Generar(c, datos);
        XNamespace pagos = "http://www.sat.gob.mx/Pagos20";
        var nodo = xml.Descendants(pagos + "Pago").Single();
        Assert.Equal(p.CadPago, (string?)nodo.Attribute("CadPago"));
        Assert.Equal("01", (string?)nodo.Attribute("TipoCadPago"));
        Assert.Equal("AA==", (string?)nodo.Attribute("CertPago"));
        Assert.Equal("AQ==", (string?)nodo.Attribute("SelloPago"));
        xml.Root!.SetAttributeValue("Sello", "AA==");
        var esquemas = new EsquemasSat(new Entorno(), Options.Create(new OpcionesDeEsquemasSat { Ruta = "CatalogosSAT" }));
        var errores = new List<string>();
        xml.Validate(esquemas.EsquemaCfdiPagos, (_, e) => errores.Add(e.Message));
        Assert.Empty(errores);
        p.FormaDePagoP = "01";
        Assert.Equal("cadena-pago-no-aplica", GeneradorDeXmlPago.Validar(c)?.Codigo);
        p.TipoCadPago = null; p.CertPago = null; p.CadPago = null; p.SelloPago = null;
        nodo = GeneradorDeXmlPago.Generar(c, datos).Descendants(pagos + "Pago").Single();
        foreach (var atributo in new[] { "TipoCadPago", "CertPago", "CadPago", "SelloPago" })
            Assert.Null(nodo.Attribute(atributo));
    }

    private sealed class Entorno : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Pruebas";
        public string ContentRootPath { get; set; } = Path.GetFullPath(Path.Combine(EsquemasSatPruebas.Raiz, ".."));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
