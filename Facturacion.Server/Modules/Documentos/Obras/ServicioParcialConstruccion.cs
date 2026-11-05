using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Obras;

namespace Facturacion.Server.Modules.Documentos.Obras;

public static class ServicioParcialConstruccion
{
    public const string Espacio = "http://www.sat.gob.mx/servicioparcialconstruccion";
    public static ServicioParcialConstruccionDto? Leer(string? json) => json is null ? null : JsonSerializer.Deserialize<ServicioParcialConstruccionDto>(json);
    public static ErrorNegocio? Validar(ServicioParcialConstruccionDto datos)
    {
        var campos = new (string? Valor, int Maximo, bool Requerido)[] {
            (datos.NumeroPermiso,40,true),(datos.Calle,150,true),(datos.Municipio,100,true),
            (datos.NoExterior,55,false),(datos.NoInterior,30,false),(datos.Colonia,100,false),
            (datos.Localidad,100,false),(datos.Referencia,100,false) };
        foreach (var (valor, maximo, requerido) in campos)
        {
            if ((requerido && string.IsNullOrWhiteSpace(valor)) || valor?.Length > maximo || valor?.Contains('|') == true)
                return Error();
            try { if (valor is not null) XmlConvert.VerifyXmlChars(valor); }
            catch (XmlException) { return Error(); }
        }
        return !Regex.IsMatch(datos.Estado ?? "", @"\A(0[1-9]|[12][0-9]|3[0-2])\z") ||
            !Regex.IsMatch(datos.CodigoPostal ?? "", @"\A[0-9]{5}\z") ? Error() : null;
    }
    public static XElement Generar(ServicioParcialConstruccionDto d)
    {
        XNamespace ns = Espacio;
        XAttribute? Opcional(string nombre, string? valor) => string.IsNullOrWhiteSpace(valor) ? null : new(nombre, valor.Trim());
        return new(ns + "parcialesconstruccion", new XAttribute("Version", "1.0"), new XAttribute("NumPerLicoAut", d.NumeroPermiso.Trim()),
            new XElement(ns + "Inmueble", new XAttribute("Calle", d.Calle.Trim()), new XAttribute("Municipio", d.Municipio.Trim()),
                new XAttribute("Estado", d.Estado), new XAttribute("CodigoPostal", d.CodigoPostal),
                Opcional("NoExterior",d.NoExterior), Opcional("NoInterior",d.NoInterior), Opcional("Colonia",d.Colonia),
                Opcional("Localidad",d.Localidad), Opcional("Referencia",d.Referencia)));
    }
    private static ErrorNegocio Error() => ErrorNegocio.Validacion("servicio-parcial-invalido",
        "Completa permiso, calle, municipio, estado de 01 a 32 y código postal del inmueble. Revisa las longitudes.");
}
