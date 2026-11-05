using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.ComercioExterior;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.ComercioExterior;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Facturacion.Pruebas;

public sealed class ComercioExteriorAduanaPruebas
{
    private static readonly GeneradorDeXmlComercioExterior Generador = new(new EsquemasSat(
        new Entorno(), Options.Create(new OpcionesDeEsquemasSat { Ruta = Path.GetFullPath(EsquemasSatPruebas.Raiz) })));

    [HechoConEsquemasSat]
    public void Mercancia_completa_genera_el_complemento_con_el_total_correcto()
        => Assert.True(Generador.GenerarElemento(Documento(), Datos()).EsExito);

    [Fact]
    public void Un_xml_sintacticamente_valido_no_acepta_un_total_usd_distinto_de_sus_mercancias()
    {
        var resultado = Generador.GenerarElemento(Documento(), Datos() with { TotalUsd = 201m });
        Assert.True(resultado.EsFallo);
        Assert.Equal("comercio-total-usd-inconsistente", resultado.Error!.Codigo);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void Cantidad_unidad_y_valor_aduaneros_se_capturan_juntos(bool cantidad, bool unidad, bool valor)
    {
        var datos = Datos();
        var mercancia = datos.Mercancias![0] with
        {
            CantidadAduana = cantidad ? 2m : null,
            UnidadAduana = unidad ? "06" : null,
            ValorUnitarioAduana = valor ? 100m : null
        };
        var resultado = Generador.GenerarElemento(Documento(), datos with { Mercancias = [mercancia] });
        Assert.True(resultado.EsFallo);
        Assert.Equal("comercio-datos-aduaneros-incompletos", resultado.Error!.Codigo);
    }

    [Fact]
    public void Los_datos_aduaneros_se_exigen_en_todas_las_mercancias_si_una_los_lleva()
    {
        var documento = Documento();
        documento.Conceptos.Add(new()
        {
            Orden = 2, NoIdentificacion = "REV-2", ClaveProdServ = "43222600", ClaveUnidad = "H87",
            Descripcion = "Segundo equipo", Cantidad = 1, ValorUnitario = 10, Importe = 10, ObjetoImp = "02"
        });
        var datos = Datos();
        var segunda = datos.Mercancias![0] with
        {
            OrdenConcepto = 2, DescripcionConcepto = "Segundo equipo", ValorDolares = 10,
            CantidadAduana = null, UnidadAduana = null, ValorUnitarioAduana = null
        };
        var resultado = Generador.GenerarElemento(documento, datos with { TotalUsd = 210, Mercancias = [datos.Mercancias[0], segunda] });
        Assert.True(resultado.EsFallo);
        Assert.Equal("comercio-datos-aduaneros-incompletos", resultado.Error!.Codigo);
    }

    [HechoConEsquemasSat]
    public void El_total_usd_redondea_la_suma_a_dos_decimales()
    {
        var datos = Datos();
        var resultado = Generador.GenerarElemento(Documento(), datos with
        {
            TotalUsd = 200.01m,
            Mercancias = [datos.Mercancias![0] with { ValorDolares = 200.005m }]
        });
        Assert.True(resultado.EsExito);
        Assert.Equal("200.01", (string?)resultado.Valor.Attribute("TotalUSD"));
    }

    [Theory]
    [InlineData("incoterm", "comercio-incoterm-obligatorio")]
    [InlineData("curp-moral", "comercio-curp-no-aplica")]
    [InlineData("curp-fisica", "comercio-curp-obligatoria")]
    [InlineData("certificado", "comercio-certificado-no-aplica")]
    [InlineData("fraccion-servicio", "comercio-fraccion-no-aplica")]
    [InlineData("sin-fraccion", "comercio-fraccion-obligatoria")]
    [InlineData("precio-cero", "comercio-valor-unitario-aduana")]
    [InlineData("valor-aduana", "comercio-valor-aduana-inconsistente")]
    [InlineData("conversion", "comercio-conversion-inconsistente")]
    public void Rechaza_inconsistencias_que_el_xsd_no_detecta(string caso, string codigo)
    {
        var documento = Documento();
        var datos = Datos();
        var mercancia = datos.Mercancias![0];
        switch (caso)
        {
            case "incoterm": datos = datos with { Incoterm = null }; break;
            case "curp-moral": datos = datos with { CurpEmisor = "EUMA840107HHGSRN04" }; break;
            case "curp-fisica": documento.EmisorRfc = "EUMA840107AA0"; break;
            case "certificado": datos = datos with { NumeroCertificadoOrigen = "CERTIFICADO" }; break;
            case "fraccion-servicio": documento.Conceptos[0].ClaveUnidad = "E48"; break;
            case "sin-fraccion": datos = datos with { Mercancias = [mercancia with { FraccionArancelaria = null }] }; break;
            case "precio-cero": datos = datos with { Mercancias = [mercancia with { ValorUnitarioAduana = 0 }] }; break;
            case "valor-aduana": datos = datos with { Mercancias = [mercancia with { ValorDolares = 190 }], TotalUsd = 190 }; break;
            case "conversion": documento.Moneda = "MXN"; break;
        }
        var resultado = Generador.GenerarElemento(documento, datos);
        Assert.True(resultado.EsFallo);
        Assert.Equal(codigo, resultado.Error!.Codigo);
    }

    [HechoConEsquemasSat]
    public void Serializa_precision_aduanera_igual_a_la_usada_en_los_limites()
    {
        var resultado = Generador.GenerarElemento(Documento(), Datos());
        Assert.True(resultado.EsExito, resultado.Error?.Mensaje);
        var mercancia = resultado.Valor.Descendants().Single(e => e.Name.LocalName == "Mercancia");
        Assert.Equal("2.000", (string?)mercancia.Attribute("CantidadAduana"));
        Assert.Equal("100.000000", (string?)mercancia.Attribute("ValorUnitarioAduana"));
    }

    [Theory]
    [InlineData("EUMA840107HHGSRN04")]
    [InlineData("EUMA840107XHGSRN04")]
    public void Curp_del_emisor_respeta_el_patron_sat_incluida_la_clave_x(string curp)
    {
        var documento = Documento();
        documento.EmisorRfc = "EUMA840107AA0";
        Assert.Null(ReglasDeComercioExterior.ValidarCabecera(documento, Datos() with { CurpEmisor = curp }, true));
    }

    private static DatosComercioExteriorDto Datos() => new("USA", "123456789", "A1", false, null, null, null,
        18.5m, 200m, null,
        new("Calle de prueba", "10", null, null, null, null, "048", "HID", "MEX", "42000"),
        new("Test street", "20", null, null, null, null, "Austin", "TX", "USA", "78701"), "CPT",
        [new(1, "43222600", "Equipo de prueba", "8517621701", "06", 2, 100, 200, null, null, null, null)]);

    private static Comprobante Documento() => new()
    {
        Estatus = "borrador", TipoDeComprobante = "I", Exportacion = "02", Moneda = "USD",
        LugarExpedicion = "42000", EmisorRfc = "EKU9003173C9", EmisorNombre = "EMISOR DE PRUEBA",
        EmisorRegimenFiscal = "601", ReceptorRfc = "XEXX010101000", ReceptorNombre = "RECEPTOR DE PRUEBA",
        ReceptorRegimenFiscal = "616", ReceptorDomicilioFiscal = "42000", ReceptorUsoCfdi = "S01",
        ReceptorResidenciaFiscal = "USA", ReceptorNumRegIdTrib = "123456789",
        Conceptos = [new()
        {
            Orden = 1, NoIdentificacion = "REV-1", ClaveProdServ = "43222600", ClaveUnidad = "H87",
            Descripcion = "Equipo de prueba", Cantidad = 2, ValorUnitario = 100, Importe = 200, ObjetoImp = "02"
        }]
    };

    private sealed class Entorno : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Pruebas";
        public string ContentRootPath { get; set; } = Path.GetFullPath(Path.Combine(EsquemasSatPruebas.Raiz, ".."));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
