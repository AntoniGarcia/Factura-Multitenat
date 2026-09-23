using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.ComercioExterior;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.ComercioExterior;

public sealed class GeneradorDeXmlComercioExterior(EsquemasSat esquemas)
{
    private static readonly XNamespace Ce = EsquemasSat.EspacioDeNombresComercioExterior20;

    public Resultado<byte[]> Generar(Comprobante comprobante, DatosComercioExteriorDto datos)
    {
        var elemento = GenerarElemento(comprobante, datos);
        if (elemento.EsFallo) return elemento.Error!;
        var documento = new XDocument(new XDeclaration("1.0", "utf-8", null), elemento.Valor);
        using var salida = new MemoryStream();
        using (var escritor = XmlWriter.Create(salida, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false), Indent = true, CloseOutput = false
        })) documento.Save(escritor);
        return salida.ToArray();
    }

    public Resultado<XElement> GenerarElemento(Comprobante comprobante, DatosComercioExteriorDto datos)
    {
        if (comprobante.Exportacion != "02" || comprobante.TipoDeComprobante != "I")
            return ErrorNegocio.Regla("comercio-tipo-invalido", "Solo se genera el complemento para facturas de exportación definitiva.");
        if (datos.ClavePedimento != "A1" || datos.TipoCambioUsd is null or <= 0 ||
            datos.TotalUsd is null or < 0 ||
            datos.CertificadoOrigen && string.IsNullOrWhiteSpace(datos.NumeroCertificadoOrigen))
            return ErrorNegocio.Validacion("comercio-cabecera-incompleta", "Completa la clave de pedimento, tipo de cambio USD, total USD y certificado de origen cuando aplique.");
        if (!DomicilioCompleto(datos.DomicilioEmisor) || !DomicilioCompleto(datos.DomicilioReceptor))
            return ErrorNegocio.Validacion("comercio-domicilio-incompleto", "Completa calle, estado, país y código postal de ambos domicilios.");

        var mercancias = datos.Mercancias ?? [];
        if (mercancias.Count == 0)
            return ErrorNegocio.Validacion("comercio-sin-mercancias", "Agrega por lo menos una mercancía exportada.");

        var conceptos = comprobante.Conceptos.ToDictionary(x => x.Orden);
        var identificadores = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mercancia in mercancias)
        {
            if (!conceptos.TryGetValue(mercancia.OrdenConcepto, out var concepto) ||
                concepto.ClaveProdServ != mercancia.ClaveProdServConcepto ||
                concepto.Descripcion != mercancia.DescripcionConcepto)
                return ErrorNegocio.Conflicto("comercio-concepto-modificado", "Los conceptos cambiaron; revisa y guarda nuevamente los datos aduaneros.");
            if (string.IsNullOrWhiteSpace(concepto.NoIdentificacion) ||
                !identificadores.Add(concepto.NoIdentificacion))
                return ErrorNegocio.Validacion("comercio-identificador-invalido", "Cada mercancía debe tener un identificador de concepto distinto.");
            if (mercancia.ValorDolares is null or < 0 ||
                decimal.Round(mercancia.ValorDolares.Value, 4) != mercancia.ValorDolares ||
                mercancia.CantidadAduana is { } cantidad &&
                    (cantidad < 0.001m || decimal.Round(cantidad, 3) != cantidad) ||
                mercancia.ValorUnitarioAduana is { } unitario &&
                    (unitario < 0 || decimal.Round(unitario, 6) != unitario) ||
                (mercancia.Modelo is not null || mercancia.Submodelo is not null || mercancia.NumeroSerie is not null) &&
                    string.IsNullOrWhiteSpace(mercancia.Marca))
                return ErrorNegocio.Validacion("comercio-mercancia-incompleta", $"Revisa valor, cantidad y descripción específica de la mercancía {mercancia.OrdenConcepto}.");
        }

        var raiz = new XElement(Ce + "ComercioExterior",
            new XAttribute(XNamespace.Xmlns + "cce20", Ce.NamespaceName),
            new XAttribute("Version", "2.0"),
            new XAttribute("ClaveDePedimento", datos.ClavePedimento),
            new XAttribute("CertificadoOrigen", datos.CertificadoOrigen ? "1" : "0"),
            Opcional("NumCertificadoOrigen", datos.NumeroCertificadoOrigen),
            Opcional("NumeroExportadorConfiable", datos.NumeroExportadorConfiable),
            Opcional("Incoterm", datos.Incoterm),
            Opcional("Observaciones", datos.Observaciones),
            new XAttribute("TipoCambioUSD", Numero(datos.TipoCambioUsd.Value)),
            new XAttribute("TotalUSD", Numero(datos.TotalUsd.Value)),
            new XElement(Ce + "Emisor", Opcional("Curp", datos.CurpEmisor),
                Domicilio(datos.DomicilioEmisor)),
            new XElement(Ce + "Receptor", Opcional("NumRegIdTrib", datos.NumeroRegistroTributario),
                Domicilio(datos.DomicilioReceptor)),
            new XElement(Ce + "Mercancias", mercancias.OrderBy(x => x.OrdenConcepto).Select(x =>
                new XElement(Ce + "Mercancia",
                    new XAttribute("NoIdentificacion", conceptos[x.OrdenConcepto].NoIdentificacion!),
                    Opcional("FraccionArancelaria", x.FraccionArancelaria),
                    NumeroOpcional("CantidadAduana", x.CantidadAduana),
                    Opcional("UnidadAduana", x.UnidadAduana),
                    NumeroOpcional("ValorUnitarioAduana", x.ValorUnitarioAduana),
                    new XAttribute("ValorDolares", Numero(x.ValorDolares!.Value)),
                    string.IsNullOrWhiteSpace(x.Marca) ? null :
                        new XElement(Ce + "DescripcionesEspecificas", new XAttribute("Marca", x.Marca),
                            Opcional("Modelo", x.Modelo), Opcional("SubModelo", x.Submodelo),
                            Opcional("NumeroSerie", x.NumeroSerie))))));

        var documento = new XDocument(new XDeclaration("1.0", "utf-8", null), raiz);
        string? errorEsquema = null;
        documento.Validate(esquemas.EsquemaComercioExterior, (_, args) =>
            errorEsquema ??= args.Message);
        if (errorEsquema is not null)
            return ErrorNegocio.Validacion("comercio-esquema-invalido", $"El complemento no cumple el esquema SAT: {errorEsquema}");

        return raiz;
    }

    private static bool DomicilioCompleto(DomicilioComercioExteriorDto? domicilio)
        => domicilio is not null && !string.IsNullOrWhiteSpace(domicilio.Calle) &&
           !string.IsNullOrWhiteSpace(domicilio.Estado) && !string.IsNullOrWhiteSpace(domicilio.Pais) &&
           !string.IsNullOrWhiteSpace(domicilio.CodigoPostal);

    private static XElement Domicilio(DomicilioComercioExteriorDto d)
        => new(Ce + "Domicilio", new XAttribute("Calle", d.Calle!),
            Opcional("NumeroExterior", d.NumeroExterior), Opcional("NumeroInterior", d.NumeroInterior),
            Opcional("Colonia", d.Colonia), Opcional("Localidad", d.Localidad),
            Opcional("Referencia", d.Referencia), Opcional("Municipio", d.Municipio),
            new XAttribute("Estado", d.Estado!), new XAttribute("Pais", d.Pais!),
            new XAttribute("CodigoPostal", d.CodigoPostal!));

    private static string Numero(decimal numero) => numero.ToString("0.######", CultureInfo.InvariantCulture);
    private static XAttribute? NumeroOpcional(string nombre, decimal? valor)
        => valor is null ? null : new XAttribute(nombre, Numero(valor.Value));
    private static XAttribute? Opcional(string nombre, string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : new XAttribute(nombre, valor);
}
