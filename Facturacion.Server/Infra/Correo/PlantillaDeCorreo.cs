using System.Net;
using System.Text.RegularExpressions;

namespace Facturacion.Server.Infra.Correo;

// Texto a HTML de correo. Lo usan las plantillas del operador y el mensaje al enviar un CFDI,
// y los dos deben escapar igual.
public static class PlantillaDeCorreo
{
    // Marcadores en mayúsculas (NOMBRE, CODIGO…), legibles para un operador sin experiencia técnica.
    public static string Rellenar(string plantilla, params (string Marcador, string Valor)[] valores)
    {
        var resultado = plantilla;

        foreach (var (marcador, valor) in valores)
            resultado = Regex.Replace(resultado, $@"\b{Regex.Escape(marcador)}\b", valor);

        return resultado;
    }

    // Sustituye con los valores en bruto y escapa todo al final, para que ningún valor cuele
    // etiquetas. Las líneas en blanco separan párrafos.
    public static string Renderizar(string plantilla, params (string Marcador, string Valor)[] valores)
    {
        var texto = Rellenar(plantilla, valores);

        return string.Join("\n",
            texto
                .Replace("\r\n", "\n")
                .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
                .Select(bloque =>
                    $"<p>{string.Join("<br>\n", bloque.Split('\n').Select(linea => Escapar(linea.TrimEnd())))}</p>"));
    }

    public static string Escapar(string texto) => WebUtility.HtmlEncode(texto);
}
