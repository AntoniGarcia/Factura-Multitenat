using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Facturacion.Shared.ComercioExterior;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>Recupera el complemento emitido sin consultar la captura editable de Comercio Exterior.</summary>
public static class DatosComercioExteriorDelPdf
{
    private static readonly XNamespace Cfdi = "http://www.sat.gob.mx/cfd/4";
    private static readonly XNamespace Comercio = EsquemasSat.EspacioDeNombresComercioExterior20;

    public static DatosComercioExteriorDto? DesdeXml(byte[] contenido)
    {
        using var lector = XmlReader.Create(new MemoryStream(contenido), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 10_000_000
        });
        var raiz = XDocument.Load(lector).Root
            ?? throw new InvalidDataException("El XML emitido está vacío.");
        var complementos = raiz.Element(Cfdi + "Complemento")?.Elements(Comercio + "ComercioExterior").ToArray() ?? [];
        if (complementos.Length == 0) return null;
        if (complementos.Length != 1)
            throw new InvalidDataException("El XML contiene más de un complemento de Comercio Exterior.");
        var complemento = complementos[0];
        var receptorCfdi = raiz.Element(Cfdi + "Receptor");
        var emisor = Requerido(complemento, "Emisor");
        var receptor = Requerido(complemento, "Receptor");
        var conceptos = new Dictionary<string, (int Orden, XElement Concepto)>(StringComparer.Ordinal);
        var orden = 0;
        foreach (var concepto in raiz.Element(Cfdi + "Conceptos")?.Elements(Cfdi + "Concepto") ?? [])
        {
            orden++;
            if (Atributo(concepto, "NoIdentificacion") is { Length: > 0 } identificador &&
                !conceptos.TryAdd(identificador, (orden, concepto)))
                throw new InvalidDataException("El XML contiene identificadores de mercancía repetidos.");
        }

        var mercancias = Requerido(complemento, "Mercancias").Elements(Comercio + "Mercancia").Select(mercancia =>
        {
            if (Atributo(mercancia, "NoIdentificacion") is not { } id || !conceptos.TryGetValue(id, out var concepto))
                throw new InvalidDataException("Una mercancía del XML no corresponde a sus conceptos.");
            var descripcion = mercancia.Element(Comercio + "DescripcionesEspecificas");
            return new MercanciaComercioExteriorDto(concepto.Orden,
                Atributo(concepto.Concepto, "ClaveProdServ")!, Atributo(concepto.Concepto, "Descripcion")!,
                Atributo(mercancia, "FraccionArancelaria"), Atributo(mercancia, "UnidadAduana"),
                Numero(mercancia, "CantidadAduana"), Numero(mercancia, "ValorUnitarioAduana"), Numero(mercancia, "ValorDolares"),
                Atributo(descripcion, "Marca"), Atributo(descripcion, "Modelo"),
                Atributo(descripcion, "SubModelo"), Atributo(descripcion, "NumeroSerie"));
        }).ToArray();

        return new DatosComercioExteriorDto(Atributo(receptorCfdi, "ResidenciaFiscal"),
            Atributo(receptor, "NumRegIdTrib"), Atributo(complemento, "ClaveDePedimento"),
            Atributo(complemento, "CertificadoOrigen") == "1", Atributo(complemento, "NumCertificadoOrigen"),
            Atributo(complemento, "NumeroExportadorConfiable"), Atributo(complemento, "Observaciones"),
            Numero(complemento, "TipoCambioUSD"), Numero(complemento, "TotalUSD"), Atributo(emisor, "Curp"),
            Domicilio(Requerido(emisor, "Domicilio")), Domicilio(Requerido(receptor, "Domicilio")),
            Atributo(complemento, "Incoterm"), mercancias);
    }

    private static DomicilioComercioExteriorDto Domicilio(XElement nodo)
        => new(Atributo(nodo, "Calle"), Atributo(nodo, "NumeroExterior"), Atributo(nodo, "NumeroInterior"),
            Atributo(nodo, "Colonia"), Atributo(nodo, "Localidad"), Atributo(nodo, "Referencia"),
            Atributo(nodo, "Municipio"), Atributo(nodo, "Estado"), Atributo(nodo, "Pais"), Atributo(nodo, "CodigoPostal"));

    private static XElement Requerido(XElement padre, string nombre)
        => padre.Element(Comercio + nombre)
            ?? throw new InvalidDataException($"El XML emitido no contiene el nodo de Comercio Exterior '{nombre}'.");

    private static string? Atributo(XElement? nodo, string nombre) => (string?)nodo?.Attribute(nombre);

    private static decimal? Numero(XElement nodo, string nombre)
        => Atributo(nodo, nombre) is { } texto
            ? decimal.Parse(texto, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)
            : null;
}
