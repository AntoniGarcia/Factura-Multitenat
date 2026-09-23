using System.Globalization;
using System.Xml.Linq;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.Salidas;

/// <summary>Integra el complemento Notarios Públicos 1.0 al CFDI de ingreso.</summary>
public sealed class GeneradorDeXmlNotaria(GeneradorDeXmlCfdi generadorCfdi)
{
    private static readonly XNamespace Cfdi = EsquemasSat.EspacioDeNombresCfdi;
    private static readonly XNamespace Notarios = EsquemasSat.EspacioDeNombresNotariosPublicos;
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    public Resultado<XDocument> Generar(
        Comprobante comprobante, DatosDeEmision emision, DatosNotaria datos, ConfiguracionNotario? notario)
    {
        var error = Validar(datos, notario);
        if (error is not null) return error;

        var documento = generadorCfdi.Generar(comprobante, emision);
        var raiz = documento.Root!;
        raiz.SetAttributeValue(XNamespace.Xmlns + "notariospublicos", Notarios.NamespaceName);
        raiz.SetAttributeValue(Xsi + "schemaLocation",
            $"{Cfdi.NamespaceName} http://www.sat.gob.mx/sitio_internet/cfd/4/cfdv40.xsd " +
            $"{Notarios.NamespaceName} http://www.sat.gob.mx/sitio_internet/cfd/notariospublicos/notariospublicos.xsd");

        var enajenantes = datos.Partes.Where(x => x.Rol == "enajenante").OrderBy(x => x.Orden).ToArray();
        var adquirentes = datos.Partes.Where(x => x.Rol == "adquirente").OrderBy(x => x.Orden).ToArray();

        raiz.Add(new XElement(Cfdi + "Complemento",
            new XElement(Notarios + "NotariosPublicos",
                new XAttribute("Version", "1.0"),
                new XElement(Notarios + "DescInmuebles", datos.Inmuebles.OrderBy(x => x.Orden).Select(x =>
                    new XElement(Notarios + "DescInmueble",
                        new XAttribute("TipoInmueble", x.TipoInmueble),
                        new XAttribute("Calle", x.Calle),
                        Opcional("NoExterior", x.NumeroExterior),
                        Opcional("NoInterior", x.NumeroInterior),
                        Opcional("Colonia", x.Colonia),
                        Opcional("Localidad", x.Localidad),
                        Opcional("Referencia", x.Referencia),
                        new XAttribute("Municipio", x.Municipio),
                        new XAttribute("Estado", x.Estado),
                        new XAttribute("Pais", x.Pais),
                        new XAttribute("CodigoPostal", x.CodigoPostal)))),
                new XElement(Notarios + "DatosOperacion",
                    new XAttribute("NumInstrumentoNotarial", datos.NumeroInstrumentoNotarial),
                    new XAttribute("FechaInstNotarial", datos.FechaInstrumentoNotarial.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    new XAttribute("MontoOperacion", Importe(datos.MontoOperacion)),
                    new XAttribute("Subtotal", Importe(datos.SubtotalOperacion)),
                    new XAttribute("IVA", Importe(datos.IvaOperacion))),
                new XElement(Notarios + "DatosNotario",
                    new XAttribute("CURP", notario!.Curp),
                    new XAttribute("NumNotaria", notario.NumeroNotaria),
                    new XAttribute("EntidadFederativa", notario.Estado),
                    Opcional("Adscripcion", notario.Adscripcion)),
                Grupo("DatosEnajenante", "DatosUnEnajenante", "DatosEnajenantesCopSC",
                    "DatosEnajenanteCopSC", datos.EnajenantesEnCopropiedad, enajenantes),
                Grupo("DatosAdquiriente", "DatosUnAdquiriente", "DatosAdquirientesCopSC",
                    "DatosAdquirienteCopSC", datos.AdquirentesEnCopropiedad, adquirentes))));

        return documento;
    }

    public static ErrorNegocio? Validar(DatosNotaria datos, ConfiguracionNotario? notario)
    {
        if (notario is null || string.IsNullOrWhiteSpace(notario.Curp) ||
            notario.NumeroNotaria is < 1 or > 999 || string.IsNullOrWhiteSpace(notario.Estado))
            return ErrorNegocio.Regla("perfil-notario-incompleto",
                "Completa la configuración del notario antes de validar o timbrar esta factura.");

        if (datos.Inmuebles.Count == 0 || datos.NumeroInstrumentoNotarial is < 1 or > 999999 ||
            datos.FechaInstrumentoNotarial == default || datos.MontoOperacion < 0 ||
            datos.SubtotalOperacion < 0 || datos.IvaOperacion < 0 ||
            datos.MontoOperacion != datos.SubtotalOperacion + datos.IvaOperacion)
            return ErrorNegocio.Regla("operacion-notarial-incompleta",
                "Revisa el instrumento, los inmuebles y los importes de la operación notarial.");

        if (!GrupoValido(datos.Partes, "enajenante", datos.EnajenantesEnCopropiedad) ||
            !GrupoValido(datos.Partes, "adquirente", datos.AdquirentesEnCopropiedad))
            return ErrorNegocio.Regla("partes-notariales-incompletas",
                "Completa y guarda vendedores y compradores antes de validar o timbrar esta factura.");

        return null;
    }

    private static bool GrupoValido(IEnumerable<ParteNotarial> partes, string rol, bool copropiedad)
    {
        var grupo = partes.Where(x => x.Rol == rol).ToArray();
        return grupo.Length > 0 && (copropiedad || grupo.Length == 1) &&
               grupo.All(x => !string.IsNullOrWhiteSpace(x.Nombre) && !string.IsNullOrWhiteSpace(x.Rfc) &&
                   (rol != "enajenante" || x.Rfc.Length == 13) &&
                   (copropiedad ? x.Porcentaje is >= 0 and <= 100 : x.Porcentaje is null)) &&
               (!copropiedad || grupo.Sum(x => x.Porcentaje ?? 0m) == 100m) &&
               (copropiedad || rol != "enajenante" ||
                grupo.All(x => !string.IsNullOrWhiteSpace(x.ApellidoPaterno) && !string.IsNullOrWhiteSpace(x.Curp)));
    }

    private static XElement Grupo(
        string nombre, string individual, string contenedor, string elemento,
        bool copropiedad, IReadOnlyList<ParteNotarial> partes)
        => new(Notarios + nombre,
            new XAttribute("CoproSocConyugalE", copropiedad ? "Si" : "No"),
            copropiedad
                ? new XElement(Notarios + contenedor, partes.Select(x => Persona(elemento, x, true)))
                : Persona(individual, partes[0], false));

    private static XElement Persona(string nombre, ParteNotarial parte, bool copropiedad)
        => new(Notarios + nombre,
            new XAttribute("Nombre", parte.Nombre),
            Opcional("ApellidoPaterno", parte.ApellidoPaterno),
            Opcional("ApellidoMaterno", parte.ApellidoMaterno),
            new XAttribute("RFC", parte.Rfc),
            Opcional("CURP", parte.Curp),
            copropiedad ? new XAttribute("Porcentaje", parte.Porcentaje!.Value.ToString("F2", CultureInfo.InvariantCulture)) : null);

    private static XAttribute? Opcional(string nombre, string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : new XAttribute(nombre, valor);

    private static string Importe(decimal valor) => valor.ToString("F6", CultureInfo.InvariantCulture);
}
