using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Salidas;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Facturacion.Pruebas;

/// <summary>
/// Marca una prueba que necesita los artefactos oficiales del SAT en <c>CatalogosSAT/</c>.
/// Si no están, la prueba se <b>omite</b> en vez de fallar: esos archivos no van al
/// repositorio —igual que el <c>.xls</c> de catálogos— y una suite que revienta en la
/// máquina de quien acaba de clonar no sirve de nada.
///
/// <para>
/// Se omite, no se aprueba en silencio: <c>dotnet test</c> las reporta como <c>Skipped</c>,
/// que es visible. Aprobarlas sin ejecutarlas sería peor que no tenerlas.
/// </para>
/// </summary>
public sealed class HechoConEsquemasSatAttribute : FactAttribute
{
    public HechoConEsquemasSatAttribute()
    {
        if (!File.Exists(Path.Combine(EsquemasSatPruebas.Raiz, "cfdv40.xsd")))
            Skip = $"Faltan los artefactos del SAT en '{EsquemasSatPruebas.Raiz}'. Ver docs/DESPLIEGUE.md.";
    }
}

/// <summary>
/// Comprueba que el XML que produce la fase B3 cumple de verdad el estándar: se valida
/// contra el <c>cfdv40.xsd</c> oficial y se le calcula la cadena original con el
/// <c>cadenaoriginal_4_0.xslt</c> oficial, no con una reimplementación.
///
/// <para>
/// Es la única prueba del proyecto que depende de archivos externos, y por eso existe el
/// atributo de arriba. Sin ella, la fase B3 se estaría creyendo su propio serializador.
/// </para>
/// </summary>
public sealed class EsquemasSatPruebas
{
    internal static readonly string Raiz = Path.Combine(
        Path.GetDirectoryName(typeof(EsquemasSatPruebas).Assembly.Location)!,
        "..", "..", "..", "..", "..", "CatalogosSAT");

    private static readonly XNamespace Cfdi = "http://www.sat.gob.mx/cfd/4";

    private static EsquemasSat Esquemas()
    {
        var entorno = new Entorno(Path.GetFullPath(Path.Combine(Raiz, "..")));
        return new EsquemasSat(entorno, Options.Create(new OpcionesDeEsquemasSat { Ruta = "CatalogosSAT" }));
    }

    private sealed class Entorno(string raiz) : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Pruebas";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = raiz;
        public string EnvironmentName { get; set; } = "Development";
    }

    private static XDocument Documento()
    {
        var comprobante = new Comprobante
        {
            Estatus = "borrador",
            Serie = "A",
            Folio = 1,
            TipoDeComprobante = "I",
            FechaEmisionUtc = new DateTime(2026, 8, 17, 18, 0, 0, DateTimeKind.Utc),
            LugarExpedicion = "42000",
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
            ReceptorDomicilioFiscal = "42000",
            ReceptorUsoCfdi = "S01",
            Conceptos =
            [
                new Concepto
                {
                    Orden = 1,
                    ClaveProdServ = "01010101",
                    ClaveUnidad = "H87",
                    Descripcion = "SERVICIO DE PRUEBA",
                    ObjetoImp = "02",
                    Cantidad = 3,
                    ValorUnitario = 1.111111m,
                    Importe = 3.333333m,
                    Impuestos =
                    [
                        new ImpuestoConcepto
                        {
                            Impuesto = "002", TipoFactor = "Tasa", TasaOCuota = 0.16m,
                            Base = 3.333333m, Importe = 0.533333m, EsRetencion = false
                        }
                    ]
                }
            ]
        };

        var datos = new DatosDeEmision(
            new DateTime(2026, 8, 17, 12, 0, 0), 2, "30001000000400002434",
            Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }));

        var documento = new GeneradorDeXmlCfdi().Generar(comprobante, datos);

        // El sello se escribe antes de validar, que es el orden real: se valida el documento
        // que viaja al PAC, no una versión intermedia.
        documento.Root!.SetAttributeValue("Sello", Convert.ToBase64String(new byte[256]));

        return documento;
    }

    [HechoConEsquemasSat]
    public void El_xml_generado_cumple_el_xsd_oficial_del_sat()
    {
        var problemas = new List<string>();

        var ajustes = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = Esquemas().Esquema,
            DtdProcessing = DtdProcessing.Prohibit
        };

        ajustes.ValidationEventHandler += (_, e) => problemas.Add(e.Message);

        using var lector = XmlReader.Create(new StringReader(Documento().ToString()), ajustes);
        while (lector.Read()) { }

        Assert.True(problemas.Count == 0, string.Join(Environment.NewLine, problemas));
    }

    [HechoConEsquemasSat]
    public void La_cadena_original_sale_del_xslt_oficial_del_sat()
    {
        var salida = new StringWriter();

        using (var lector = Documento().CreateReader())
        using (var escritor = XmlWriter.Create(salida, new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment
        }))
        {
            Esquemas().CadenaOriginal.Transform(lector, escritor);
        }

        var cadena = salida.ToString();

        // La cadena original del SAT empieza y termina con doble pleca, y lleva los datos
        // separados por una. No se comprueba su contenido completo —lo define el XSLT, no
        // esta prueba— sino que se produjo y trae lo que identifica al comprobante.
        Assert.StartsWith("||", cadena, StringComparison.Ordinal);
        Assert.EndsWith("||", cadena, StringComparison.Ordinal);
        Assert.Contains("|4.0|", cadena, StringComparison.Ordinal);
        Assert.Contains("EKU9003173C9", cadena, StringComparison.Ordinal);
        Assert.Contains("XAXX010101000", cadena, StringComparison.Ordinal);
    }

    /// <summary>
    /// Que el XSLT compile es lo que estuvo en duda: incluye 33 XSLT de complemento por URL
    /// absoluta, y sin el resolutor local cada cadena original saldría a internet.
    /// </summary>
    [HechoConEsquemasSat]
    public void El_xslt_compila_resolviendo_sus_treinta_y_tres_includes_en_local()
        => Assert.NotNull(Esquemas().CadenaOriginal);

    /// <summary>Falta un archivo: revienta diciendo cuál, no devuelve un documento vacío.</summary>
    [Fact]
    public void Un_esquema_ausente_falla_diciendo_que_falta()
    {
        var resolutor = new ResolutorDeEsquemasSat(Path.Combine(Path.GetTempPath(), "no-existe-" + Guid.NewGuid()));

        var error = Assert.Throws<FileNotFoundException>(() =>
            resolutor.GetEntity(new Uri("http://www.sat.gob.mx/sitio_internet/cfd/4/cfdv40.xsd"), null, typeof(Stream)));

        Assert.Contains("cfdv40.xsd", error.Message, StringComparison.Ordinal);
        Assert.Contains("DESPLIEGUE", error.Message, StringComparison.Ordinal);
    }
}
