using System.Globalization;
using System.Text;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Server.Modules.Plataforma.Productos;

/// <summary>
/// Convierte el CSV que trae el contador en renglones revisables. <b>No guarda nada</b>: su
/// trabajo es que el usuario vea qué va a entrar y qué no <b>antes</b> de confirmar.
///
/// <para><b>Por qué un renglón malo no aborta el archivo</b></para>
/// Van a llegar archivos de mil productos exportados de un Excel viejo. Si el renglón 700
/// tiene una coma de más y eso tira la importación completa, el contador no adopta el
/// sistema. Cada renglón se valida por su cuenta y los buenos entran.
///
/// <para><b>Impuestos en el CSV</b></para>
/// Pedir las tres columnas del SAT en un archivo que alguien captura a mano sería una
/// trampa. El CSV usa una columna <c>Iva</c> con los valores que un contador reconoce
/// —<c>16</c>, <c>8</c>, <c>0</c>, <c>exento</c>— y aquí se traducen al modelo completo.
/// Quien necesite retenciones o IEPS las configura en la pantalla del producto.
/// </para>
/// </summary>
public static class LectorDeCsvDeProductos
{
    /// <summary>Encabezados que se esperan, en este orden.</summary>
    public static readonly string[] Columnas =
        ["ClaveProdServ", "ClaveUnidad", "Unidad", "Descripcion", "Precio", "PesoKg", "ObjetoImp", "Iva"];

    /// <summary>Tope de renglones por archivo, para que una importación no tumbe el servidor.</summary>
    private const int MaximoRenglones = 5000;

    public static VistaPreviaDeImportacion Analizar(Stream archivo)
    {
        // Detecta la marca de orden de bytes: los CSV que salen de Excel la traen, y sin
        // esto el primer encabezado llegaría con caracteres invisibles y no coincidiría.
        using var lector = new StreamReader(archivo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var renglones = new List<RenglonDeImportacion>();
        var numero = 0;
        var primera = true;

        while (lector.ReadLine() is { } linea)
        {
            if (primera)
            {
                primera = false;

                // Se salta el encabezado solo si de verdad lo es; si el archivo viene sin
                // encabezado, el primer renglón son datos y hay que leerlo.
                if (EsEncabezado(linea)) continue;
            }

            if (string.IsNullOrWhiteSpace(linea)) continue;

            numero++;

            if (numero > MaximoRenglones)
            {
                renglones.Add(new RenglonDeImportacion(numero, null,
                    $"El archivo pasa de {MaximoRenglones} renglones. Pártelo en varios."));
                break;
            }

            renglones.Add(LeerRenglon(numero, linea));
        }

        return new VistaPreviaDeImportacion(
            renglones,
            renglones.Count(r => r.Error is null),
            renglones.Count(r => r.Error is not null));
    }

    private static bool EsEncabezado(string linea)
        => linea.Contains("ClaveProdServ", StringComparison.OrdinalIgnoreCase)
           || linea.Contains("Descripcion", StringComparison.OrdinalIgnoreCase)
           || linea.Contains("Descripción", StringComparison.OrdinalIgnoreCase);

    private static RenglonDeImportacion LeerRenglon(int numero, string linea)
    {
        var campos = PartirCsv(linea);

        if (campos.Count < 7)
            return new RenglonDeImportacion(numero, null,
                $"Se esperaban al menos 7 columnas y llegaron {campos.Count}.");

        var claveProdServ = campos[0].Trim();
        var claveUnidad = campos[1].Trim();
        var unidad = campos[2].Trim();
        var descripcion = campos[3].Trim();

        if (claveProdServ.Length == 0) return Malo(numero, "Falta la clave de producto o servicio.");
        if (claveUnidad.Length == 0) return Malo(numero, "Falta la clave de unidad.");
        if (descripcion.Length == 0) return Malo(numero, "Falta la descripción.");
        if (unidad.Length == 0) unidad = claveUnidad;

        if (!TryLeerDecimal(campos[4], out var precio))
            return Malo(numero, $"El precio «{campos[4]}» no es un número.");

        decimal? peso = null;
        if (!string.IsNullOrWhiteSpace(campos[5]))
        {
            if (!TryLeerDecimal(campos[5], out var pesoLeido))
                return Malo(numero, $"El peso «{campos[5]}» no es un número.");

            peso = pesoLeido;
        }

        var objetoImp = campos[6].Trim();
        if (objetoImp.Length == 0) return Malo(numero, "Falta el objeto de impuesto.");

        var iva = campos.Count > 7 ? campos[7].Trim() : string.Empty;

        var impuestos = TraducirIva(iva, objetoImp, out var errorIva);
        if (errorIva is not null) return Malo(numero, errorIva);

        return new RenglonDeImportacion(numero,
            new PeticionGuardarProducto(
                claveProdServ, claveUnidad, unidad, descripcion, precio, peso, objetoImp, impuestos, Activo: true),
            null);
    }

    /// <summary>
    /// Traduce la columna amigable a las tres del SAT. «16» y «16%» y «0.16» significan lo
    /// mismo para quien captura, así que las tres se aceptan.
    /// </summary>
    private static IReadOnlyList<ImpuestoDeProductoDto> TraducirIva(
        string iva, string objetoImp, out string? error)
    {
        error = null;

        // Objeto de impuesto sin desglose: el renglón no lleva impuestos, y si el archivo
        // trae algo en la columna es que el contador se contradijo.
        if (objetoImp is "01" or "03")
        {
            if (!string.IsNullOrWhiteSpace(iva) && !iva.Equals("no", StringComparison.OrdinalIgnoreCase))
                error = $"El objeto de impuesto {objetoImp} no lleva desglose, pero la columna Iva dice «{iva}».";

            return [];
        }

        if (string.IsNullOrWhiteSpace(iva))
        {
            error = "Falta la columna Iva. Escribe 16, 8, 0 o exento.";
            return [];
        }

        // 002 es la clave del IVA en c_Impuesto.
        if (iva.Equals("exento", StringComparison.OrdinalIgnoreCase))
            return [new ImpuestoDeProductoDto("002", "Exento", null, EsRetencion: false)];

        var limpio = iva.Replace("%", string.Empty).Trim();

        if (!TryLeerDecimal(limpio, out var valor))
        {
            error = $"El IVA «{iva}» no se entiende. Escribe 16, 8, 0 o exento.";
            return [];
        }

        // «16» quiere decir 16 %, no 1600 %. Por debajo de 1 se toma como fracción ya escrita.
        var tasa = valor > 1 ? valor / 100m : valor;

        return [new ImpuestoDeProductoDto("002", "Tasa", tasa, EsRetencion: false)];
    }

    private static RenglonDeImportacion Malo(int numero, string error)
        => new(numero, null, error);

    private static bool TryLeerDecimal(string texto, out decimal valor)
    {
        var limpio = (texto ?? string.Empty).Trim().Replace("$", string.Empty).Replace(",", string.Empty);

        return decimal.TryParse(limpio, NumberStyles.Any, CultureInfo.InvariantCulture, out valor);
    }

    /// <summary>
    /// Parte un renglón de CSV respetando las comillas: una descripción como
    /// «Caja, 12 piezas» viene entrecomillada y no debe partirse en dos columnas.
    /// </summary>
    private static List<string> PartirCsv(string linea)
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
}
