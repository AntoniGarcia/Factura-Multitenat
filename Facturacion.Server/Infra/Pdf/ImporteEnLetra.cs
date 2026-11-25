using System.Globalization;
using System.Text;

namespace Facturacion.Server.Infra.Pdf;

/// <summary>
/// Convierte un importe monetario a su representación en letra para representación impresa
/// en español de México (ej. <c>TRESCIENTOS CUARENTA Y UN MIL CUATROCIENTOS PESOS 00/100 M.N.</c>).
/// </summary>
public static class ImporteEnLetra
{
    private static readonly string[] Unidades =
    [
        "", "UN", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE",
        "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE",
        "VEINTE", "VEINTIÚN", "VEINTIDÓS", "VEINTITRÉS", "VEINTICUATRO", "VEINTICINCO", "VEINTISÉIS", "VEINTISIETE", "VEINTIOCHO", "VEINTINUEVE"
    ];

    private static readonly string[] Decenas =
    [
        "", "", "", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"
    ];

    private static readonly string[] Centenas =
    [
        "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS",
        "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"
    ];

    public static string Convertir(decimal monto, string moneda)
    {
        var negativo = monto < 0;
        var absoluto = Math.Round(Math.Abs(monto), 2, MidpointRounding.AwayFromZero);
        var enteros = (long)Math.Truncate(absoluto);
        var centavos = (int)Math.Round((absoluto - enteros) * 100m, 0, MidpointRounding.AwayFromZero);
        if (centavos == 100)
        {
            enteros++;
            centavos = 0;
        }

        var textoEnteros = enteros == 0 ? "CERO" : EnteroALetras(enteros);
        var clave = (moneda ?? "MXN").Trim().ToUpperInvariant();
        var (singular, plural, sufijo) = clave switch
        {
            "MXN" => ("PESO", "PESOS", "M.N."),
            "USD" => ("DÓLAR", "DÓLARES", "USD"),
            "EUR" => ("EURO", "EUROS", "EUR"),
            _ => (clave, clave, clave)
        };

        var unidadMoneda = enteros == 1 ? singular : plural;
        var de = enteros > 0 && enteros % 1_000_000 == 0 ? "DE " : string.Empty;
        var prefijo = negativo ? "MENOS " : string.Empty;
        return $"{prefijo}{textoEnteros} {de}{unidadMoneda} {centavos:00}/100 {sufijo}".Trim();
    }

    private static string EnteroALetras(long numero)
    {
        var partes = new List<string>();

        var milesDeMillones = numero / 1_000_000_000;
        var resto = numero % 1_000_000_000;
        var millones = resto / 1_000_000;
        resto %= 1_000_000;
        var miles = resto / 1_000;
        var unidades = (int)(resto % 1_000);

        if (milesDeMillones > 0)
        {
            partes.Add(milesDeMillones == 1 ? "MIL" : $"{Grupo((int)milesDeMillones)} MIL");
            if (millones == 0) partes.Add("MILLONES");
        }

        if (millones > 0)
            partes.Add(millones == 1 && milesDeMillones == 0 ? "UN MILLÓN" : $"{Grupo((int)millones)} MILLONES");

        if (miles > 0)
            partes.Add(miles == 1 ? "MIL" : $"{Grupo((int)miles)} MIL");

        if (unidades > 0)
            partes.Add(Grupo(unidades));

        return string.Join(' ', partes);
    }

    private static string Grupo(int numero)
    {
        if (numero == 0) return string.Empty;
        if (numero == 100) return "CIEN";

        var sb = new StringBuilder();
        var centena = numero / 100;
        var resto = numero % 100;

        if (centena > 0)
            sb.Append(Centenas[centena]);

        if (resto > 0)
        {
            if (sb.Length > 0) sb.Append(' ');
            if (resto < 30)
            {
                sb.Append(Unidades[resto]);
            }
            else
            {
                var decena = resto / 10;
                var unidad = resto % 10;
                sb.Append(Decenas[decena]);
                if (unidad > 0)
                    sb.Append(" Y ").Append(Unidades[unidad]);
            }
        }

        return sb.ToString();
    }
}
