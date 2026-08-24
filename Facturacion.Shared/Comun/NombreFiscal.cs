using System.Globalization;
using System.Text;

namespace Facturacion.Shared.Comun;

/// <summary>
/// Normaliza un nombre o razón social como exige CFDI 4.0: mayúsculas, sin acentos y sin
/// régimen de capital (ARQUITECTURA.md §7). Es la causa más común de rechazo del PAC, porque el
/// nombre tiene que coincidir <b>exactamente</b> con la Constancia de Situación Fiscal.
///
/// <para><b>Por qué vive en Shared y no en el servidor</b></para>
/// El <c>Client</c> la usa para enseñarle al capturista, mientras escribe, en qué va a
/// quedar convertido lo que tecleó. La que cuenta es la del servidor, que se ejecuta
/// siempre (ARQUITECTURA.md §3); tenerla en un solo lugar es lo que garantiza que las dos digan
/// lo mismo. La usan el emisor (fase 4) y el receptor (fase 5).
///
/// <para><b>La eñe no es un acento</b></para>
/// Quitar diacríticos a la brava convierte «PEÑA» en «PENA», y eso <b>rompe</b> el
/// timbrado: la Ñ es una letra propia del español y el SAT la conserva en la constancia.
/// Por eso <see cref="QuitarAcentos"/> descompone letra por letra y deja la Ñ intacta.
/// </summary>
public static class NombreFiscal
{
    /// <summary>
    /// Régimen de capital a recortar, en forma canónica: mayúsculas, sin puntos y separado
    /// en palabras. Se prueban del más largo al más corto para que «SA DE CV» no se coma
    /// solo el «SA» y deje un «DE CV» huérfano.
    /// <para>
    /// Es deliberadamente una lista cerrada y no una expresión regular: recortar de más el
    /// nombre de una empresa también rompe el timbrado, y una lista se puede revisar de un
    /// vistazo.
    /// </para>
    /// </summary>
    private static readonly string[][] RegimenesDeCapital =
    [
        ["S", "EN", "C", "POR", "A"],
        ["S", "DE", "RL", "DE", "CV"],
        ["SC", "DE", "RL", "DE", "CV"],
        ["SPR", "DE", "RL", "DE", "CV"],
        ["S", "DE", "PR", "DE", "RL"],
        ["SAPI", "DE", "CV"],
        ["SAB", "DE", "CV"],
        ["SAS", "DE", "CV"],
        ["SRL", "DE", "CV"],
        ["SPR", "DE", "RL"],
        ["SC", "DE", "RL"],
        ["S", "DE", "RL"],
        ["SA", "DE", "CV"],
        ["S", "EN", "C"],
        ["SOFOM", "ENR"],
        ["SOFOM"],
        ["SAPI"],
        ["SRL"],
        ["SNC"],
        ["SCS"],
        ["SCL"],
        ["SAS"],
        ["IAP"],
        ["SA"],
        ["SC"],
        ["AC"]
    ];

    /// <summary>
    /// Devuelve el nombre listo para el CFDI y la lista de ajustes que se le hicieron, en
    /// español, para poder explicárselos al usuario en vez de cambiarle el texto a
    /// escondidas.
    /// </summary>
    public static NombreFiscalNormalizado Normalizar(string? nombre)
    {
        var original = (nombre ?? string.Empty).Trim();
        var ajustes = new List<string>();

        var sinAcentos = QuitarAcentos(original);
        if (!string.Equals(sinAcentos, original, StringComparison.Ordinal))
            ajustes.Add("Se quitaron los acentos.");

        var mayusculas = sinAcentos.ToUpperInvariant();
        if (!string.Equals(mayusculas, sinAcentos, StringComparison.Ordinal))
            ajustes.Add("Se convirtió a mayúsculas.");

        var compacto = CompactarEspacios(mayusculas);
        if (!string.Equals(compacto, mayusculas, StringComparison.Ordinal))
            ajustes.Add("Se quitaron los espacios de sobra.");

        var (sinRegimen, recortado) = QuitarRegimenDeCapital(compacto);
        if (recortado is not null)
            ajustes.Add($"Se quitó el régimen de capital «{recortado}», que no va en el nombre del CFDI.");

        return new NombreFiscalNormalizado(original, sinRegimen, ajustes);
    }

    /// <summary>
    /// Se descompone <b>letra por letra</b> en vez de todo el texto de golpe: así la Ñ se
    /// deja pasar entera y nunca llega a descomponerse en «N + tilde», que es lo que la
    /// convertiría en N al descartar los diacríticos.
    /// </summary>
    private static string QuitarAcentos(string texto)
    {
        if (texto.Length == 0) return texto;

        var construccion = new StringBuilder(texto.Length);

        foreach (var letra in texto)
        {
            if (letra is 'Ñ' or 'ñ')
            {
                construccion.Append(letra);
                continue;
            }

            foreach (var pieza in letra.ToString().Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(pieza) != UnicodeCategory.NonSpacingMark)
                    construccion.Append(pieza);
        }

        return construccion.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string CompactarEspacios(string texto)
        => string.Join(' ', texto.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>
    /// Recorta el régimen de capital del final, cuantas veces haga falta: hay razones
    /// sociales que arrastran dos, como «… SA DE CV SOFOM ENR».
    /// </summary>
    private static (string Nombre, string? Recortado) QuitarRegimenDeCapital(string nombre)
    {
        var palabras = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var recortadas = new List<string>();

        while (true)
        {
            var coincidencia = RegimenesDeCapital.FirstOrDefault(regimen => TerminaEn(palabras, regimen));

            // Nunca dejar el nombre vacío: una empresa que se llame literalmente «AC» tiene
            // más derecho a su nombre que esta regla a recortarlo.
            if (coincidencia is null || palabras.Count <= coincidencia.Length) break;

            recortadas.InsertRange(0, palabras.TakeLast(coincidencia.Length));
            palabras.RemoveRange(palabras.Count - coincidencia.Length, coincidencia.Length);
        }

        if (recortadas.Count == 0) return (nombre, null);

        // La coma que separaba el régimen se queda colgando al recortarlo («CREDITOS ÑANDU,»)
        // y no aparece así en la constancia. Solo se recorta cuando de verdad hubo régimen:
        // si el usuario escribió una coma final por su cuenta, no es asunto nuestro.
        var limpio = string.Join(' ', palabras).TrimEnd(',', ';', '.', ' ');

        return (limpio, string.Join(' ', recortadas));
    }

    /// <summary>
    /// Compara ignorando puntos y comas, para que «S.A. de C.V.», «SA DE CV» y «S.A. DE C.V.»
    /// se reconozcan como el mismo régimen.
    /// </summary>
    private static bool TerminaEn(List<string> palabras, string[] regimen)
    {
        if (palabras.Count < regimen.Length) return false;

        var desplazamiento = palabras.Count - regimen.Length;

        for (var i = 0; i < regimen.Length; i++)
            if (!string.Equals(SinPuntuacion(palabras[desplazamiento + i]), regimen[i], StringComparison.Ordinal))
                return false;

        return true;
    }

    private static string SinPuntuacion(string palabra)
        => palabra.Replace(".", string.Empty).Replace(",", string.Empty);
}

/// <summary>Resultado de normalizar un nombre fiscal.</summary>
/// <param name="Original">Lo que escribió el usuario, sin tocar.</param>
/// <param name="Normalizado">Lo que va a viajar al CFDI.</param>
/// <param name="Ajustes">
/// Qué se le cambió y por qué, en frases entendibles por un contador. Vacío si el nombre
/// ya venía como lo exige el SAT.
/// </param>
public sealed record NombreFiscalNormalizado(
    string Original,
    string Normalizado,
    IReadOnlyList<string> Ajustes)
{
    /// <summary>Verdadero si hubo que cambiar algo; es lo que decide si se le avisa al usuario.</summary>
    public bool HuboCambios => Ajustes.Count > 0;
}
