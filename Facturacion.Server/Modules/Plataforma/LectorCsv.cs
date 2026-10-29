using System.Text;

namespace Facturacion.Server.Modules.Plataforma;

/// <summary>Lo común a los CSV con que se importan los catálogos de clientes y productos.</summary>
internal static class LectorCsv
{
    /// <summary>
    /// Parte un renglón de CSV respetando las comillas: un nombre como «ACME, SA» viene
    /// entrecomillado y no debe partirse en dos columnas.
    /// </summary>
    public static List<string> Partir(string linea)
    {
        var campos = new List<string>();
        var actual = new StringBuilder();
        var entreComillas = false;

        for (var i = 0; i < linea.Length; i++)
        {
            var caracter = linea[i];

            if (entreComillas)
            {
                if (caracter != '"') { actual.Append(caracter); continue; }

                // Dos comillas seguidas dentro de un campo son una comilla literal.
                if (i + 1 < linea.Length && linea[i + 1] == '"') { actual.Append('"'); i++; }
                else entreComillas = false;

                continue;
            }

            switch (caracter)
            {
                case '"':
                    entreComillas = true;
                    break;
                case ',':
                    campos.Add(actual.ToString());
                    actual.Clear();
                    break;
                default:
                    actual.Append(caracter);
                    break;
            }
        }

        campos.Add(actual.ToString());

        return campos;
    }

    /// <summary>Lee la columna Activo: Sí, No y sus equivalentes que salen de otros sistemas.</summary>
    public static bool TryLeerSiNo(string texto, out bool valor)
    {
        switch (texto.Trim().ToUpperInvariant())
        {
            case "SÍ" or "SI" or "TRUE" or "1":
                valor = true;
                return true;
            case "NO" or "FALSE" or "0":
                valor = false;
                return true;
            default:
                valor = false;
                return false;
        }
    }
}
