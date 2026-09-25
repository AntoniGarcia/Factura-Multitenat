using System.Xml;
using System.Xml.Schema;
using System.Xml.Xsl;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>Dónde están los artefactos oficiales del SAT en el disco.</summary>
public sealed class OpcionesDeEsquemasSat
{
    public const string Seccion = "EsquemasSat";

    /// <summary>
    /// Carpeta con <c>cfdv40.xsd</c>, sus importados y <c>cadenaoriginal_4_0.xslt</c>. Los
    /// XSLT de complemento van en la subcarpeta <c>xslt</c>. No están en el repositorio: son
    /// artefactos del SAT, como el <c>.xls</c> de catálogos (ver docs/DESPLIEGUE.md).
    /// </summary>
    public string Ruta { get; init; } = "CatalogosSAT";
}

/// <summary>
/// Resuelve las referencias del SAT a los archivos locales.
///
/// <para><b>Por qué hace falta</b></para>
/// Tanto <c>cfdv40.xsd</c> como <c>cadenaoriginal_4_0.xslt</c> referencian por URL absoluta
/// —<c>http://www.sat.gob.mx/…</c>— lo que importan. Sin este resolutor, cada validación y
/// cada cadena original saldrían a internet: lento, y sobre todo dependiente de que el
/// portal del SAT esté arriba justo cuando alguien timbra. Peor aún, una llamada de red
/// dentro del sellado es una llamada de red en el camino crítico del timbrado.
///
/// <para><b>Falla ruidosa a propósito</b></para>
/// Si falta un archivo, revienta diciendo cuál y de dónde bajarlo. La alternativa —devolver
/// un documento vacío— compilaría igual y produciría cadenas originales incompletas, que se
/// traducen en sellos inválidos que solo se descubren cuando el PAC rechaza.
/// </summary>
public sealed class ResolutorDeEsquemasSat(string raiz) : XmlResolver
{
    public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        var nombre = absoluteUri.Segments.Length > 0
            ? Uri.UnescapeDataString(absoluteUri.Segments[^1])
            : string.Empty;

        foreach (var candidata in new[] { Path.Combine(raiz, nombre), Path.Combine(raiz, "xslt", nombre) })
        {
            if (File.Exists(candidata)) return File.OpenRead(candidata);
        }

        throw new FileNotFoundException(
            $"Falta el archivo '{nombre}' que el SAT referencia como '{absoluteUri}'. " +
            $"Se busca en '{raiz}' y en '{Path.Combine(raiz, "xslt")}'. " +
            "Se descarga del portal del SAT; ver docs/DESPLIEGUE.md.");
    }
}

/// <summary>
/// Carga y conserva el esquema y la transformación del SAT.
///
/// <para>
/// Los dos son caros de compilar y no cambian mientras el proceso viva, así que se arman una
/// sola vez. Es la razón de que este servicio sea <c>Singleton</c>: compilar el XSLT en cada
/// timbrado añadiría cientos de milisegundos a la operación que más prisa tiene.
/// </para>
/// </summary>
public sealed class EsquemasSat
{
    /// <summary>Espacio de nombres del CFDI 4.0. Es el prefijo <c>cfdi</c> del XML.</summary>
    public const string EspacioDeNombresCfdi = "http://www.sat.gob.mx/cfd/4";
    public const string EspacioDeNombresCartaPorte31 = "http://www.sat.gob.mx/CartaPorte31";
    public const string EspacioDeNombresNotariosPublicos = "http://www.sat.gob.mx/notariospublicos";
    public const string EspacioDeNombresComercioExterior20 = "http://www.sat.gob.mx/ComercioExterior20";
    public const string EspacioDeNombresImpuestosLocales = "http://www.sat.gob.mx/implocal";

    private readonly string _raiz;
    private readonly Lazy<XmlSchemaSet> _esquema;
    private readonly Lazy<XmlSchemaSet> _esquemaCartaPorte;
    private readonly Lazy<XmlSchemaSet> _esquemaNotaria;
    private readonly Lazy<XmlSchemaSet> _esquemaComercioExterior;
    private readonly Lazy<XmlSchemaSet> _esquemaCfdiComercioExterior;
    private readonly Lazy<XmlSchemaSet> _esquemaCfdiImpuestosLocales;
    private readonly Lazy<XslCompiledTransform> _cadenaOriginal;

    public EsquemasSat(IHostEnvironment entorno, Microsoft.Extensions.Options.IOptions<OpcionesDeEsquemasSat> opciones)
    {
        var configurada = opciones.Value.Ruta;

        _raiz = Path.IsPathRooted(configurada)
            ? configurada
            : Path.Combine(entorno.ContentRootPath, configurada);

        // Lazy y no en el constructor: el servidor tiene que poder arrancar sin los archivos
        // del SAT. Quien intente timbrar sin ellos recibe el error; quien solo entre a ver
        // clientes, no.
        _esquema = new Lazy<XmlSchemaSet>(CargarEsquema);
        _esquemaCartaPorte = new Lazy<XmlSchemaSet>(CargarEsquemaCartaPorte);
        _esquemaNotaria = new Lazy<XmlSchemaSet>(CargarEsquemaNotaria);
        _esquemaComercioExterior = new Lazy<XmlSchemaSet>(CargarEsquemaComercioExterior);
        _esquemaCfdiComercioExterior = new Lazy<XmlSchemaSet>(CargarEsquemaCfdiComercioExterior);
        _esquemaCfdiImpuestosLocales = new Lazy<XmlSchemaSet>(CargarEsquemaCfdiImpuestosLocales);
        _cadenaOriginal = new Lazy<XslCompiledTransform>(CargarCadenaOriginal);
    }

    public XmlSchemaSet Esquema => _esquema.Value;

    /// <summary>Esquema de CFDI 4.0 con el complemento Carta Porte 3.1.</summary>
    public XmlSchemaSet EsquemaCartaPorte => _esquemaCartaPorte.Value;

    public XmlSchemaSet EsquemaNotaria => _esquemaNotaria.Value;

    public XmlSchemaSet EsquemaComercioExterior => _esquemaComercioExterior.Value;

    public XmlSchemaSet EsquemaCfdiComercioExterior => _esquemaCfdiComercioExterior.Value;

    public XmlSchemaSet EsquemaCfdiImpuestosLocales => _esquemaCfdiImpuestosLocales.Value;

    public XslCompiledTransform CadenaOriginal => _cadenaOriginal.Value;

    private XmlSchemaSet CargarEsquema() => CargarEsquema(incluirCartaPorte: false, incluirNotaria: false, incluirComercio: false, incluirImpuestosLocales: false);

    private XmlSchemaSet CargarEsquemaCartaPorte() => CargarEsquema(incluirCartaPorte: true, incluirNotaria: false, incluirComercio: false, incluirImpuestosLocales: false);

    private XmlSchemaSet CargarEsquemaNotaria() => CargarEsquema(incluirCartaPorte: false, incluirNotaria: true, incluirComercio: false, incluirImpuestosLocales: false);

    private XmlSchemaSet CargarEsquemaCfdiComercioExterior()
        => CargarEsquema(incluirCartaPorte: false, incluirNotaria: false, incluirComercio: true, incluirImpuestosLocales: false);

    private XmlSchemaSet CargarEsquemaCfdiImpuestosLocales()
        => CargarEsquema(incluirCartaPorte: false, incluirNotaria: false, incluirComercio: false, incluirImpuestosLocales: true);

    private XmlSchemaSet CargarEsquemaComercioExterior()
    {
        var ruta = Path.Combine(_raiz, "ComercioExterior20.xsd");
        if (!File.Exists(ruta))
            throw new FileNotFoundException(
                $"Falta 'ComercioExterior20.xsd' en '{_raiz}'. Descárgalo del portal del SAT.", ruta);

        var resolutor = new ResolutorDeEsquemasSat(_raiz);
        var conjunto = new XmlSchemaSet { XmlResolver = resolutor };
        using var lector = XmlReader.Create(ruta, new XmlReaderSettings
        {
            XmlResolver = resolutor, DtdProcessing = DtdProcessing.Prohibit
        });
        conjunto.Add(EspacioDeNombresComercioExterior20, lector);
        conjunto.Compile();
        return conjunto;
    }

    private XmlSchemaSet CargarEsquema(bool incluirCartaPorte, bool incluirNotaria, bool incluirComercio, bool incluirImpuestosLocales)
    {
        var principal = Path.Combine(_raiz, "cfdv40.xsd");
        var cartaPorte = Path.Combine(_raiz, "CartaPorte31.xsd");
        var notaria = Path.Combine(_raiz, "notariospublicos.xsd");
        var comercio = Path.Combine(_raiz, "ComercioExterior20.xsd");
        var impuestosLocales = Path.Combine(_raiz, "implocal.xsd");

        if (!File.Exists(principal))
            throw new FileNotFoundException(
                $"Falta 'cfdv40.xsd' en '{_raiz}'. Se descarga del portal del SAT; ver docs/DESPLIEGUE.md.",
                principal);

        if (incluirCartaPorte && !File.Exists(cartaPorte))
            throw new FileNotFoundException(
                $"Falta 'CartaPorte31.xsd' en '{_raiz}'. Se descarga del portal del SAT; ver docs/DESPLIEGUE.md.",
                cartaPorte);

        if (incluirNotaria && !File.Exists(notaria))
            throw new FileNotFoundException(
                $"Falta 'notariospublicos.xsd' en '{_raiz}'. Se descarga del portal del SAT; ver docs/DESPLIEGUE.md.",
                notaria);

        if (incluirComercio && !File.Exists(comercio))
            throw new FileNotFoundException(
                $"Falta 'ComercioExterior20.xsd' en '{_raiz}'. Descárgalo del portal del SAT.", comercio);

        if (incluirImpuestosLocales && !File.Exists(impuestosLocales))
            throw new FileNotFoundException(
                $"Falta 'implocal.xsd' en '{_raiz}'. Descárgalo del portal del SAT.", impuestosLocales);

        var conjunto = new XmlSchemaSet { XmlResolver = new ResolutorDeEsquemasSat(_raiz) };

        using var lector = XmlReader.Create(principal, new XmlReaderSettings
        {
            XmlResolver = new ResolutorDeEsquemasSat(_raiz),
            DtdProcessing = DtdProcessing.Prohibit
        });

        // Con el espacio de nombres explícito y no null: el esquema declara el suyo, y
        // pasar null hace que XmlSchemaSet lo tome como discrepancia y falle al agregarlo.
        conjunto.Add(EspacioDeNombresCfdi, lector);

        if (incluirCartaPorte)
        {
            using var lectorCartaPorte = XmlReader.Create(cartaPorte, new XmlReaderSettings
            {
                XmlResolver = new ResolutorDeEsquemasSat(_raiz),
                DtdProcessing = DtdProcessing.Prohibit
            });
            conjunto.Add(EspacioDeNombresCartaPorte31, lectorCartaPorte);
        }
        if (incluirNotaria)
        {
            using var lectorNotaria = XmlReader.Create(notaria, new XmlReaderSettings
            {
                XmlResolver = new ResolutorDeEsquemasSat(_raiz),
                DtdProcessing = DtdProcessing.Prohibit
            });
            conjunto.Add(EspacioDeNombresNotariosPublicos, lectorNotaria);
        }
        if (incluirComercio)
        {
            using var lectorComercio = XmlReader.Create(comercio, new XmlReaderSettings
            {
                XmlResolver = new ResolutorDeEsquemasSat(_raiz),
                DtdProcessing = DtdProcessing.Prohibit
            });
            conjunto.Add(EspacioDeNombresComercioExterior20, lectorComercio);
        }
        if (incluirImpuestosLocales)
        {
            using var lectorImpuestosLocales = XmlReader.Create(impuestosLocales, new XmlReaderSettings
            {
                XmlResolver = new ResolutorDeEsquemasSat(_raiz),
                DtdProcessing = DtdProcessing.Prohibit
            });
            conjunto.Add(EspacioDeNombresImpuestosLocales, lectorImpuestosLocales);
        }
        conjunto.Compile();

        return conjunto;
    }

    private XslCompiledTransform CargarCadenaOriginal()
    {
        var principal = Path.Combine(_raiz, "cadenaoriginal_4_0.xslt");

        if (!File.Exists(principal))
            throw new FileNotFoundException(
                $"Falta 'cadenaoriginal_4_0.xslt' en '{_raiz}'. Se descarga del portal del SAT; " +
                "incluye 33 XSLT de complemento que van en la subcarpeta 'xslt'. Ver docs/DESPLIEGUE.md.",
                principal);

        var transformacion = new XslCompiledTransform();

        // enableDocumentFunction en false: la transformación no tiene por qué abrir documentos
        // que no sean los suyos, y el resolutor ya acota de dónde salen sus includes.
        transformacion.Load(
            XmlReader.Create(principal, new XmlReaderSettings
            {
                XmlResolver = new ResolutorDeEsquemasSat(_raiz),
                DtdProcessing = DtdProcessing.Prohibit
            }),
            new XsltSettings(enableDocumentFunction: false, enableScript: false),
            new ResolutorDeEsquemasSat(_raiz));

        return transformacion;
    }
}
