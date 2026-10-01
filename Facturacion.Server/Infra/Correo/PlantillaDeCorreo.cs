using System.Net;
using System.Text.RegularExpressions;

namespace Facturacion.Server.Infra.Correo;

/// <summary>
/// Convierte texto normal en el HTML de un correo: las plantillas del operador en el registro
/// y el mensaje libre que el usuario escribe al enviar un CFDI. Vive aparte porque lo usan
/// los dos, y los dos tienen que escapar exactamente igual.
/// </summary>
public static class PlantillaDeCorreo
{
    /// <summary>
    /// Sustituye los marcadores de una plantilla del operador. Son palabras completas en
    /// mayúsculas (NOMBRE, CODIGO, CORREO…), no corchetes ni llaves: un operador sin
    /// experiencia técnica puede leer el mensaje tal y como quedará.
    /// </summary>
    public static string Rellenar(string plantilla, params (string Marcador, string Valor)[] valores)
    {
        var resultado = plantilla;

        foreach (var (marcador, valor) in valores)
            resultado = Regex.Replace(resultado, $@"\b{Regex.Escape(marcador)}\b", valor);

        return resultado;
    }

    /// <summary>
    /// Se sustituyen primero los marcadores (con los valores en bruto, aún sin escapar) y se
    /// escapa y convierte todo al final: así un nombre o un correo raros no pueden colar
    /// etiquetas dentro del mensaje, por mucho que la plantilla esté en manos de otro.
    /// Las líneas en blanco separan párrafos.
    /// </summary>
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
