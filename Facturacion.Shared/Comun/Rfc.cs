using System.Text.RegularExpressions;

namespace Facturacion.Shared.Comun;

/// <summary>
/// Validación de RFC: formato del anexo 20 <b>y</b> dígito verificador.
///
/// <para><b>Por qué el dígito verificador importa</b></para>
/// El formato solo dice que el RFC «tiene forma de RFC». El dígito verificador es lo que
/// atrapa el error real del capturista —una letra cambiada, dos dígitos volteados—, porque
/// un RFC mal tecleado casi nunca cae en un dígito verificador correcto. Sin esta
/// comprobación, el error se descubre hasta que el PAC rechaza el comprobante, cuando ya se
/// apartó un folio (CLAUDE.md §5).
///
/// <para><b>Vive en Shared para que Client y Server no puedan discrepar</b></para>
/// El <c>Client</c> la usa para avisarle al capturista mientras escribe; la que cuenta es la
/// del servidor, que se ejecuta siempre (CLAUDE.md §3). Tener dos implementaciones sería
/// tener dos verdades.
/// </summary>
public static class Rfc
{
    /// <summary>Público en general. Ver <see cref="EsGenerico"/> para por qué se exceptúa.</summary>
    public const string GenericoNacional = "XAXX010101000";

    /// <summary>Residente en el extranjero.</summary>
    public const string GenericoExtranjero = "XEXX010101000";

    /// <summary>
    /// Tabla del SAT para el dígito verificador: la posición de cada carácter <b>es</b> su
    /// valor. La Ñ y el &amp; existen porque forman parte de razones sociales reales, y el
    /// espacio ocupa el 37 porque es con lo que se rellena un RFC de doce a trece.
    /// </summary>
    private const string Diccionario = "0123456789ABCDEFGHIJKLMN&OPQRSTUVWXYZ Ñ";

    /// <summary>
    /// Forma del anexo 20: tres letras (moral) o cuatro (física), fecha AAMMDD y homoclave
    /// de tres. El último carácter solo puede ser dígito o «A», que es todo lo que el
    /// algoritmo del dígito verificador puede producir.
    /// </summary>
    private static readonly Regex Forma = new(
        @"^[A-ZÑ&]{3,4}[0-9]{2}[0-1][0-9][0-3][0-9][A-Z0-9]{2}[0-9A]$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Doce caracteres es persona moral; trece, persona física.</summary>
    public static bool EsPersonaMoral(string rfc) => rfc.Length == 12;

    /// <summary>
    /// Los dos RFC genéricos del SAT. <b>Se exceptúan del dígito verificador a propósito</b>:
    /// <c>XAXX010101000</c> no lo cumple —su dígito calculado es 4, no 0—, y aun así es el
    /// que el SAT obliga a usar para público en general. Rechazarlo por «inválido» sería
    /// impedir facturar al mostrador.
    /// </summary>
    public static bool EsGenerico(string? rfc)
        => rfc is GenericoNacional or GenericoExtranjero;

    public static ValidacionRfc Validar(string? rfc)
    {
        var valor = (rfc ?? string.Empty).Trim().ToUpperInvariant();

        if (valor.Length == 0)
            return new ValidacionRfc(false, "Escribe el RFC.");

        if (EsGenerico(valor))
            return new ValidacionRfc(true, null);

        if (valor.Length is not (12 or 13))
            return new ValidacionRfc(false,
                "El RFC debe tener 12 caracteres si es persona moral, o 13 si es persona física.");

        if (!Forma.IsMatch(valor))
            return new ValidacionRfc(false, "El RFC no tiene la forma que exige el SAT.");

        if (!FechaValida(valor))
            return new ValidacionRfc(false, "La fecha dentro del RFC no existe.");

        var esperado = DigitoVerificador(valor);

        if (valor[^1] != esperado)
            return new ValidacionRfc(false,
                "El RFC está mal escrito: el último carácter no corresponde. Revísalo contra la constancia.");

        return new ValidacionRfc(true, null);
    }

    /// <summary>
    /// Calcula el dígito verificador de un RFC. Recibe el RFC completo y usa solo lo que va
    /// antes del último carácter, que es justamente el dígito a comprobar.
    /// <para>
    /// Es público porque la prueba obligatoria de la fase 5 lo ejercita directamente, y
    /// porque poder preguntar «cuál debería ser» ayuda a explicar el error.
    /// </para>
    /// </summary>
    public static char DigitoVerificador(string rfc)
    {
        // Un RFC de persona moral se rellena a trece con un espacio a la izquierda: la
        // tabla le da el valor 37 y así las dos longitudes usan la misma fórmula.
        var completo = rfc.Length == 12 ? " " + rfc : rfc;

        var suma = 0;

        for (var i = 0; i < 12; i++)
        {
            var valor = Diccionario.IndexOf(completo[i]);

            // Un carácter fuera de la tabla no puede producir un dígito válido; se devuelve
            // algo que nunca coincidirá en vez de fingir un resultado.
            if (valor < 0) return '\0';

            suma += valor * (13 - i);
        }

        var residuo = suma % 11;

        return residuo switch
        {
            0 => '0',
            1 => 'A',
            _ => (char)('0' + (11 - residuo))
        };
    }

    /// <summary>
    /// La fecha del RFC viene sin siglo, así que solo se comprueba que el día exista dentro
    /// del mes. El 29 de febrero se acepta: sin siglo no hay forma de saber si el año era
    /// bisiesto, y rechazarlo dejaría fuera a contribuyentes reales.
    /// </summary>
    private static bool FechaValida(string rfc)
    {
        var inicio = EsPersonaMoral(rfc) ? 3 : 4;

        var mes = int.Parse(rfc.AsSpan(inicio + 2, 2));
        var dia = int.Parse(rfc.AsSpan(inicio + 4, 2));

        if (mes is < 1 or > 12) return false;

        var diasDelMes = mes switch
        {
            2 => 29,
            4 or 6 or 9 or 11 => 30,
            _ => 31
        };

        return dia >= 1 && dia <= diasDelMes;
    }
}

/// <summary>Resultado de validar un RFC.</summary>
/// <param name="EsValido">Verdadero si pasa forma, fecha y dígito verificador.</param>
/// <param name="Mensaje">Qué está mal, en una frase para un contador. Nulo si es válido.</param>
public sealed record ValidacionRfc(bool EsValido, string? Mensaje);
