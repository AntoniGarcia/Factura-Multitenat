using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Facturacion.Server.Data.Entidades.Documentos;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>Datos que se imprimen del complemento, sin depender del perfil notarial actual.</summary>
public sealed record DatosNotarialesDelPdf(
    int NumeroInstrumento,
    DateOnly FechaInstrumento,
    decimal MontoOperacion,
    decimal Subtotal,
    decimal Iva,
    string CurpNotario,
    int NumeroNotaria,
    string EstadoNotaria,
    string? Adscripcion,
    IReadOnlyList<InmuebleDelPdf> Inmuebles,
    IReadOnlyList<ParteNotarialDelPdf> Enajenantes,
    IReadOnlyList<ParteNotarialDelPdf> Adquirentes)
{
    private static readonly XNamespace Notarios = EsquemasSat.EspacioDeNombresNotariosPublicos;

    public static DatosNotarialesDelPdf DesdeBorrador(DatosNotaria datos, ConfiguracionNotario notario)
        => new(
            datos.NumeroInstrumentoNotarial, datos.FechaInstrumentoNotarial,
            datos.MontoOperacion, datos.SubtotalOperacion, datos.IvaOperacion,
            notario.Curp, notario.NumeroNotaria, notario.Estado, notario.Adscripcion,
            [.. datos.Inmuebles.OrderBy(x => x.Orden).Select(x => new InmuebleDelPdf(
                x.TipoInmueble, x.Calle, x.NumeroExterior, x.NumeroInterior, x.Colonia,
                x.Localidad, x.Referencia, x.Municipio, x.Estado, x.Pais, x.CodigoPostal))],
            [.. datos.Partes.Where(x => x.Rol == "enajenante").OrderBy(x => x.Orden)
                .Select(DesdeParte)],
            [.. datos.Partes.Where(x => x.Rol == "adquirente").OrderBy(x => x.Orden)
                .Select(DesdeParte)]);

    /// <summary>El PDF fiscal lee el XML ya timbrado; cambiar el perfil no reescribe el pasado.</summary>
    public static DatosNotarialesDelPdf? DesdeXml(byte[] xml)
    {
        using var flujo = new MemoryStream(xml);
        using var lector = XmlReader.Create(flujo, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 10_000_000
        });
        var documento = XDocument.Load(lector);
        var complemento = documento.Descendants(Notarios + "NotariosPublicos").SingleOrDefault();
        if (complemento is null) return null;

        var operacion = Requerido(complemento, "DatosOperacion");
        var notario = Requerido(complemento, "DatosNotario");
        var inmuebles = Requerido(complemento, "DescInmuebles")
            .Elements(Notarios + "DescInmueble")
            .Select(x => new InmuebleDelPdf(
                Atributo(x, "TipoInmueble"), Atributo(x, "Calle"), Opcional(x, "NoExterior"),
                Opcional(x, "NoInterior"), Opcional(x, "Colonia"), Opcional(x, "Localidad"),
                Opcional(x, "Referencia"), Atributo(x, "Municipio"), Atributo(x, "Estado"),
                Atributo(x, "Pais"), Atributo(x, "CodigoPostal")))
            .ToArray();

        return new DatosNotarialesDelPdf(
            int.Parse(Atributo(operacion, "NumInstrumentoNotarial"), CultureInfo.InvariantCulture),
            DateOnly.ParseExact(Atributo(operacion, "FechaInstNotarial"), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            decimal.Parse(Atributo(operacion, "MontoOperacion"), CultureInfo.InvariantCulture),
            decimal.Parse(Atributo(operacion, "Subtotal"), CultureInfo.InvariantCulture),
            decimal.Parse(Atributo(operacion, "IVA"), CultureInfo.InvariantCulture),
            Atributo(notario, "CURP"),
            int.Parse(Atributo(notario, "NumNotaria"), CultureInfo.InvariantCulture),
            Atributo(notario, "EntidadFederativa"), Opcional(notario, "Adscripcion"),
            inmuebles,
            Personas(Requerido(complemento, "DatosEnajenante"), "DatosUnEnajenante",
                "DatosEnajenantesCopSC", "DatosEnajenanteCopSC"),
            Personas(Requerido(complemento, "DatosAdquiriente"), "DatosUnAdquiriente",
                "DatosAdquirientesCopSC", "DatosAdquirienteCopSC"));
    }

    private static ParteNotarialDelPdf DesdeParte(ParteNotarial x)
        => new(x.Nombre, x.ApellidoPaterno, x.ApellidoMaterno, x.Rfc, x.Curp, x.Porcentaje);

    private static IReadOnlyList<ParteNotarialDelPdf> Personas(
        XElement grupo, string individual, string contenedor, string elemento)
    {
        var nodos = grupo.Element(Notarios + contenedor)?.Elements(Notarios + elemento)
            ?? grupo.Elements(Notarios + individual);
        return [.. nodos.Select(x => new ParteNotarialDelPdf(
            Atributo(x, "Nombre"), Opcional(x, "ApellidoPaterno"), Opcional(x, "ApellidoMaterno"),
            Atributo(x, "RFC"), Opcional(x, "CURP"),
            Opcional(x, "Porcentaje") is { } porcentaje
                ? decimal.Parse(porcentaje, CultureInfo.InvariantCulture) : null))];
    }

    private static XElement Requerido(XElement padre, string nombre)
        => padre.Element(Notarios + nombre)
            ?? throw new InvalidDataException($"El XML timbrado no contiene el nodo notarial '{nombre}'.");

    private static string Atributo(XElement elemento, string nombre)
        => (string?)elemento.Attribute(nombre)
            ?? throw new InvalidDataException($"El XML timbrado no contiene el atributo notarial '{nombre}'.");

    private static string? Opcional(XElement elemento, string nombre)
        => (string?)elemento.Attribute(nombre);
}

public sealed record InmuebleDelPdf(
    string Tipo, string Calle, string? NumeroExterior, string? NumeroInterior, string? Colonia,
    string? Localidad, string? Referencia, string Municipio, string Estado, string Pais, string CodigoPostal)
{
    public string Direccion
    {
        get
        {
            var partes = new[] { Calle, NumeroExterior, NumeroInterior, Colonia, Localidad,
                Municipio, Estado, Pais, $"C. P. {CodigoPostal}" };
            return string.Join(", ", partes.Where(x => !string.IsNullOrWhiteSpace(x)));
        }
    }
}

public sealed record ParteNotarialDelPdf(
    string Nombre, string? ApellidoPaterno, string? ApellidoMaterno,
    string Rfc, string? Curp, decimal? Porcentaje)
{
    public string NombreCompleto
    {
        get
        {
            var partes = new[] { Nombre, ApellidoPaterno, ApellidoMaterno };
            return string.Join(" ", partes.Where(x => !string.IsNullOrWhiteSpace(x)));
        }
    }
}
