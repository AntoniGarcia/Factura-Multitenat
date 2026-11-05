using System.Xml.Linq;
using System.Xml.Schema;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Data.Entidades.Transporte;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Server.Modules.Documentos.Traslados;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Facturacion.Pruebas;

public sealed class CartaPortePeligrosidadPruebas
{
    [Theory]
    [InlineData("0", null, null)]
    [InlineData("0", "No", null)]
    [InlineData("0,1", "No", "No")]
    [InlineData("0,1", "Sí", "Sí")]
    [InlineData("1", "Sí", "Sí")]
    public void Normaliza_la_declaracion_sin_suponer_peligrosidad(string indicador, string? declaracion, string? esperado)
    {
        var resultado = ValidadorFiscalCartaPorte.ValidarPeligrosidad(indicador, declaracion);
        Assert.True(resultado.EsExito, resultado.Error?.Mensaje);
        Assert.Equal(esperado, resultado.Valor);
    }

    [Theory]
    [InlineData("0,1", null, "mercancia-peligrosidad-pendiente")]
    [InlineData("1", "No", "mercancia-peligrosidad-incompatible")]
    [InlineData("1", null, "mercancia-peligrosidad-pendiente")]
    [InlineData("0", "Sí", "mercancia-peligrosidad-incompatible")]
    [InlineData(null, "No", "catalogo-porte-sin-indicador")]
    [InlineData("0", "Tal vez", "mercancia-peligrosidad-invalida")]
    public void Rechaza_datos_que_dejarian_un_complemento_incompleto(string? indicador, string? declaracion, string codigo)
        => Assert.Equal(codigo, ValidadorFiscalCartaPorte.ValidarPeligrosidad(indicador, declaracion).Error?.Codigo);

    [Fact]
    public void Un_cambio_del_catalogo_exige_revisar_el_borrador_guardado()
        => Assert.Equal("mercancia-clasificacion-cambiada", ValidadorFiscalCartaPorte.ValidarPeligrosidad("0", "No", true).Error?.Codigo);

    [HechoConEsquemasSat]
    public void Xml_carta_porte_conserva_no_y_lo_omite_cuando_no_corresponde()
    {
        XNamespace cp = "http://www.sat.gob.mx/CartaPorte31";
        var traslado = Traslado();
        var datos = new DatosDeEmision(DateTime.Now, 0, "30001000000400002434", "TUVCNjQ=");
        var generador = new GeneradorDeXmlCartaPorte(new GeneradorDeXmlCfdi());
        var xml = generador.Generar(Documento(), datos, traslado, TimeZoneInfo.Utc);
        Assert.True(xml.EsExito, xml.Error?.Mensaje);
        Assert.Equal("No", (string?)xml.Valor.Descendants(cp + "Mercancia").Single().Attribute("MaterialPeligroso"));
        xml.Valor.Root!.SetAttributeValue("Sello", "AA==");
        var esquemas = new EsquemasSat(new Entorno(), Options.Create(new OpcionesDeEsquemasSat { Ruta = "CatalogosSAT" }));
        var errores = new List<string>();
        xml.Valor.Validate(esquemas.EsquemaCartaPorte, (_, e) => errores.Add(e.Message));
        Assert.Empty(errores);
        traslado.Mercancias[0].MaterialPeligroso = null;
        xml = generador.Generar(Documento(), datos, traslado, TimeZoneInfo.Utc);
        Assert.Null(xml.Valor.Descendants(cp + "Mercancia").Single().Attribute("MaterialPeligroso"));
    }

    [Theory]
    [InlineData("0", 0, null)]
    [InlineData("0", 1, "configuracion-sin-remolques")]
    [InlineData("1", 0, "remolque-obligatorio")]
    [InlineData("1", 1, null)]
    [InlineData("1", 2, null)]
    [InlineData("0,1", 0, null)]
    [InlineData("0,1", 2, null)]
    [InlineData(null, 0, "configuracion-porte-sin-datos")]
    public void Respeta_la_condicion_del_catalogo_vehicular(string? indicador, int cantidad, string? codigo)
        => Assert.Equal(codigo, ReglasDeRemolques.ValidarConfiguracion(indicador, cantidad)?.Codigo);

    [Fact]
    public void Placas_normalizadas_sin_repeticiones_y_con_limite_de_dos_remolques()
    {
        Assert.Equal("ABC1234", ReglasDeRemolques.NormalizarPlaca(" ab-c 1234 "));
        Assert.Null(ReglasDeRemolques.ValidarDatos([new("CTR004", "ABC1234"), new("CTR003", "DEF1234")]));
        Assert.Equal("remolque-repetido", ReglasDeRemolques.ValidarDatos([new("CTR004", "ABC1234"), new("CTR003", "ab-c 1234")])?.Codigo);
        Assert.Equal("remolque-invalido", ReglasDeRemolques.ValidarDatos([new("CTR004", "AB12")])?.Codigo);
        Assert.Equal("remolque-invalido", ReglasDeRemolques.ValidarDatos([new("CTR004", "AB$123")])?.Codigo);
        Assert.Equal("remolques-excedidos", ReglasDeRemolques.ValidarDatos([new("CTR004", "ABC1234"), new("CTR003", "DEF1234"), new("CTR004", "GHI1234")])?.Codigo);
    }

    [HechoConEsquemasSat]
    public void Xml_incluye_dos_remolques_ordenados_y_cumple_el_xsd()
    {
        XNamespace cp = "http://www.sat.gob.mx/CartaPorte31";
        var traslado = Traslado();
        traslado.VehiculoConfiguracionAutotransporte = "T3S2R4";
        traslado.Remolques = [new() { Orden = 2, SubTipoRem = "CTR004", Placa = "DEF1234" },
            new() { Orden = 1, SubTipoRem = "CTR003", Placa = "ABC1234" }];
        var generador = new GeneradorDeXmlCartaPorte(new GeneradorDeXmlCfdi());
        var xml = generador.Generar(Documento(), new DatosDeEmision(DateTime.Now, 0, "30001000000400002434", "TUVCNjQ="), traslado, TimeZoneInfo.Utc);
        Assert.True(xml.EsExito, xml.Error?.Mensaje);
        Assert.Equal(new[] { "ABC1234", "DEF1234" }, xml.Valor.Descendants(cp + "Remolque").Select(x => (string?)x.Attribute("Placa")));
        xml.Valor.Root!.SetAttributeValue("Sello", "AA==");
        var esquemas = new EsquemasSat(new Entorno(), Options.Create(new OpcionesDeEsquemasSat { Ruta = "CatalogosSAT" }));
        var errores = new List<string>();
        xml.Valor.Validate(esquemas.EsquemaCartaPorte, (_, e) => errores.Add(e.Message));
        Assert.Empty(errores);
        traslado.Remolques.Clear();
        xml = generador.Generar(Documento(), new DatosDeEmision(DateTime.Now, 0, "30001000000400002434", "TUVCNjQ="), traslado, TimeZoneInfo.Utc);
        Assert.Empty(xml.Valor.Descendants(cp + "Remolques"));
    }

    [Fact]
    public void Ficha_y_seguro_son_condicionales_y_no_permiten_datos_incompatibles()
    {
        var m = new Facturacion.Shared.Transporte.MercanciaCartaPorteDto(1, "01010101", "Prueba", 1, "H87", 1, MaterialPeligroso: "Sí");
        Assert.Equal("material-peligroso-incompleto", ReglasDeMaterialPeligroso.ValidarFicha(m)?.Codigo);
        m = m with { CveMaterialPeligroso = "0004", Embalaje = "1A1", DescripEmbalaje = "Bidón sellado" };
        Assert.Null(ReglasDeMaterialPeligroso.ValidarFicha(m));
        Assert.Equal("material-peligroso-no-corresponde", ReglasDeMaterialPeligroso.ValidarFicha(m with { MaterialPeligroso = "No" })?.Codigo);
        Assert.Equal("descripcion-embalaje-invalida", ReglasDeMaterialPeligroso.ValidarFicha(m with { DescripEmbalaje = "No|válido" })?.Codigo);
        Assert.Equal("seguro-ambiental-incompleto", ReglasDeMaterialPeligroso.ValidarSeguro(true, null, null)?.Codigo);
        Assert.Equal("seguro-ambiental-incompleto", ReglasDeMaterialPeligroso.ValidarSeguro(true, new string('A', 51), "POLIZA")?.Codigo);
        Assert.Equal("seguro-ambiental-incompleto", ReglasDeMaterialPeligroso.ValidarSeguro(true, "SEGURO", new string('A', 31))?.Codigo);
        Assert.Null(ReglasDeMaterialPeligroso.ValidarSeguro(true, "SEGURO", "POLIZA"));
        Assert.Null(ReglasDeMaterialPeligroso.ValidarSeguro(false, null, null));
        Assert.Equal("seguro-ambiental-no-corresponde", ReglasDeMaterialPeligroso.ValidarSeguro(false, "SEGURO", "POLIZA")?.Codigo);
    }

    [HechoConEsquemasSat]
    public void Xml_material_peligroso_incluye_ficha_y_seguro_y_valida_xsd()
    {
        XNamespace cp = "http://www.sat.gob.mx/CartaPorte31";
        var traslado = Traslado();
        traslado.Mercancias[0].MaterialPeligroso = "Sí";
        traslado.Mercancias[0].CveMaterialPeligroso = "0004";
        traslado.Mercancias[0].Embalaje = "1A1";
        traslado.Mercancias[0].DescripEmbalaje = "Bidón sellado";
        traslado.AseguraMedAmbiente = "ASEGURADORA AMBIENTAL";
        traslado.PolizaMedAmbiente = "POLIZA123";
        var generador = new GeneradorDeXmlCartaPorte(new GeneradorDeXmlCfdi());
        var datos = new DatosDeEmision(DateTime.Now, 0, "30001000000400002434", "TUVCNjQ=");
        var xml = generador.Generar(Documento(), datos, traslado, TimeZoneInfo.Utc);
        Assert.True(xml.EsExito, xml.Error?.Mensaje);
        Assert.Equal("0004", (string?)xml.Valor.Descendants(cp + "Mercancia").Single().Attribute("CveMaterialPeligroso"));
        Assert.Equal("1A1", (string?)xml.Valor.Descendants(cp + "Mercancia").Single().Attribute("Embalaje"));
        Assert.Equal("POLIZA123", (string?)xml.Valor.Descendants(cp + "Seguros").Single().Attribute("PolizaMedAmbiente"));
        xml.Valor.Root!.SetAttributeValue("Sello", "AA==");
        var errores = new List<string>();
        var esquemas = new EsquemasSat(new Entorno(), Options.Create(new OpcionesDeEsquemasSat { Ruta = "CatalogosSAT" }));
        xml.Valor.Validate(esquemas.EsquemaCartaPorte, (_, e) => errores.Add(e.Message));
        Assert.Empty(errores);
        traslado.PolizaMedAmbiente = null;
        Assert.Equal("seguro-ambiental-incompleto", generador.Generar(Documento(), datos, traslado, TimeZoneInfo.Utc).Error?.Codigo);
    }

    private static Comprobante Documento() => new()
    {
        Estatus = "borrador", TipoDeComprobante = "T", Moneda = "XXX", Exportacion = "01", LugarExpedicion = "42000", FechaEmisionUtc = DateTime.UtcNow,
        EmisorRfc = "EKU9003173C9", EmisorNombre = "ESCUELA KEMPER URGATE", EmisorRegimenFiscal = "601",
        ReceptorRfc = "EKU9003173C9", ReceptorNombre = "ESCUELA KEMPER URGATE", ReceptorRegimenFiscal = "601", ReceptorDomicilioFiscal = "42000", ReceptorUsoCfdi = "S01",
        Conceptos = [new() { Orden = 1, ClaveProdServ = "78101800", ClaveUnidad = "ACT", Descripcion = "Traslado de mercancía propia", Cantidad = 1, ObjetoImp = "01" }]
    };

    private static TrasladoCartaPorte Traslado() => new()
    {
        IdCcp = "CCC11111-1111-4111-8111-111111111111", VehiculoConfiguracionAutotransporte = "C2", VehiculoPlaca = "ABC1234", VehiculoAnioModelo = 2024,
        VehiculoPesoBruto = 3.5m, VehiculoAseguradora = "PRUEBA", VehiculoPoliza = "POLIZA100", VehiculoTipoPermiso = "TPAF01", VehiculoNumeroPermiso = "PERMISO100",
        FiguraTipo = "01", FiguraRfc = "GODE561231GR8", FiguraNombre = "OPERADOR PRUEBA", FiguraNumeroLicencia = "LICENCIA123", DistanciaRecorridaKm = 10,
        FechaSalidaUtc = DateTime.UtcNow, FechaLlegadaUtc = DateTime.UtcNow.AddHours(1),
        Ubicaciones = [new() { Tipo = "Origen", Orden = 1, RfcRemitenteDestinatario = "EKU9003173C9", Calle = "Calle", NumeroExterior = "1", Estado = "HID", Municipio = "048", CodigoPostal = "42000" },
            new() { Tipo = "Destino", Orden = 2, RfcRemitenteDestinatario = "EKU9003173C9", Calle = "Calle", NumeroExterior = "2", Estado = "HID", Municipio = "048", CodigoPostal = "42000" }],
        Mercancias = [new() { Orden = 1, ClaveProdServ = "01010101", Descripcion = "Mercancía revisada", Cantidad = 1, ClaveUnidad = "H87", PesoEnKg = 10, MaterialPeligroso = "No" }]
    };

    private sealed class Entorno : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Pruebas";
        public string ContentRootPath { get; set; } = Path.GetFullPath(Path.Combine(EsquemasSatPruebas.Raiz, ".."));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
