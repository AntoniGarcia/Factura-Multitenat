using System.Text;

namespace Facturacion.Server.Infra.ActualizacionBase;

public static class LotesDeSql
{
    public static IReadOnlyList<string> Separar(string texto)
    {
        var lotes = new List<string>();
        var lote = new StringBuilder();
        var comilla = '\0';
        var comentario = 0;
        foreach (var linea in texto.Replace("\r\n", "\n").Split('\n'))
        {
            var visible = new StringBuilder();
            if (comilla != '\0') visible.Append('~');
            for (var i = 0; i < linea.Length; i++)
            {
                var c = linea[i];
                var siguiente = i + 1 < linea.Length ? linea[i + 1] : '\0';
                if (comentario > 0)
                {
                    if (c == '/' && siguiente == '*') { comentario++; i++; }
                    else if (c == '*' && siguiente == '/') { comentario--; i++; }
                    continue;
                }
                if (comilla != '\0')
                {
                    if (c == comilla)
                    {
                        if (siguiente == comilla) i++;
                        else comilla = '\0';
                    }
                    continue;
                }
                if (c == '-' && siguiente == '-') break;
                if (c == '/' && siguiente == '*') { comentario++; i++; continue; }
                if (c is '\'' or '"' or '[')
                {
                    comilla = c == '[' ? ']' : c;
                    // Una cadena no cuenta como GO aunque esté sola en la línea.
                    visible.Append('~');
                    continue;
                }
                visible.Append(c);
            }
            var instruccion = visible.ToString().Trim();
            if (instruccion.Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                if (lote.ToString().Trim() is { Length: > 0 } anterior) lotes.Add(anterior);
                lote.Clear();
                continue;
            }
            if (System.Text.RegularExpressions.Regex.IsMatch(instruccion, @"(?i)^GO\s+\d|\bUSE\b|^:|^!!"))
                throw new InvalidDataException("El script debe usar la base configurada y no incluir comandos de SQLCMD ni GO con repeticiones.");
            lote.AppendLine(linea);
        }
        if (comentario != 0 || comilla != '\0') throw new InvalidDataException("El script contiene texto sin cerrar.");
        if (lote.ToString().Trim() is { Length: > 0 } ultimo) lotes.Add(ultimo);
        if (lotes.Count == 0) throw new InvalidDataException("El script está vacío.");
        return lotes;
    }
}
