using Facturacion.Shared.Comun;

namespace Facturacion.Pruebas;

/// <summary>
/// La prueba obligatoria de la fase 5 (ARQUITECTURA.md §6 y PROMPT-FASES-A.md): validación de RFC
/// y dígito verificador.
///
/// <para><b>De dónde salen los RFC «válidos»</b></para>
/// Dos son <b>reales y públicos</b>, y sirven de ancla independiente del algoritmo:
/// <list type="bullet">
///   <item><description><c>EKU9003173C9</c> — Escuela Kemper Urgate, el contribuyente de pruebas oficial del SAT (persona moral).</description></item>
///   <item><description><c>CACX7605101P8</c> — persona física de uso común en pruebas de CFDI.</description></item>
/// </list>
/// Los otros dos se construyeron calculando su dígito con el algoritmo <b>después</b> de que
/// los dos anteriores confirmaran que el algoritmo es correcto. El orden importa: si todos
/// los casos salieran del propio algoritmo, la prueba sería circular y pasaría aunque la
/// fórmula estuviera mal.
/// </summary>
public sealed class ValidacionDeRfcPruebas
{
    [Theory]
    // ── Dos personas morales válidas (12 caracteres) ───────────────────────────────────
    [InlineData("EKU9003173C9")]
    [InlineData("SAT970701NN3")]
    // ── Dos personas físicas válidas (13 caracteres) ───────────────────────────────────
    [InlineData("CACX7605101P8")]
    [InlineData("MOGA8001015B9")]
    public void Acepta_rfc_validos(string rfc)
        => Assert.True(Rfc.Validar(rfc).EsValido, $"Se rechazó el RFC válido {rfc}.");

    /// <summary>
    /// Los dos genéricos. <c>XEXX010101000</c> sí cumple el dígito verificador;
    /// <c>XAXX010101000</c> <b>no</b> —le tocaría 4 y termina en 0— y aun así el SAT obliga a
    /// usarlo para público en general. Se acepta por excepción explícita, no por accidente.
    /// </summary>
    [Theory]
    [InlineData("XAXX010101000")]
    [InlineData("XEXX010101000")]
    public void Acepta_los_dos_rfc_genericos(string rfc)
        => Assert.True(Rfc.Validar(rfc).EsValido, $"Se rechazó el RFC genérico {rfc}.");

    /// <summary>
    /// Es el caso que justifica toda esta prueba: la excepción del genérico nacional tiene
    /// que ser una excepción de verdad, no el resultado de que el algoritmo esté mal.
    /// </summary>
    [Fact]
    public void El_generico_nacional_no_cumple_el_digito_y_por_eso_se_exceptua()
    {
        Assert.Equal('4', Rfc.DigitoVerificador(Rfc.GenericoNacional));
        Assert.Equal('0', Rfc.GenericoNacional[^1]);

        Assert.True(Rfc.Validar(Rfc.GenericoNacional).EsValido);
    }

    [Fact]
    public void Rechaza_una_homoclave_mal_calculada()
    {
        // Mismo RFC del SAT con el último carácter cambiado: es exactamente el error de
        // dedo que el dígito verificador existe para atrapar.
        var resultado = Rfc.Validar("EKU9003173C8");

        Assert.False(resultado.EsValido);
        Assert.Contains("último carácter", resultado.Mensaje);
    }

    [Theory]
    [InlineData("EKU900317", "muy corto")]
    [InlineData("EKU9003173C99", "catorce caracteres")]
    [InlineData("EK19003173C9", "un dígito donde va una letra")]
    [InlineData("EKU9013173C9", "mes 13")]
    [InlineData("EKU9003323C9", "día 32")]
    [InlineData("", "vacío")]
    public void Rechaza_formatos_invalidos(string rfc, string porque)
        => Assert.False(Rfc.Validar(rfc).EsValido, $"Se aceptó un RFC inválido ({porque}): {rfc}");

    /// <summary>La Ñ es válida dentro de un RFC y no debe romper el cálculo.</summary>
    [Fact]
    public void Acepta_la_enie_en_las_letras()
        => Assert.True(Rfc.Validar("PEÑL920315QQA").EsValido);

    [Fact]
    public void Normaliza_minusculas_y_espacios()
        => Assert.True(Rfc.Validar("  eku9003173c9  ").EsValido);

    [Fact]
    public void Distingue_persona_moral_de_fisica_por_la_longitud()
    {
        Assert.True(Rfc.EsPersonaMoral("EKU9003173C9"));
        Assert.False(Rfc.EsPersonaMoral("CACX7605101P8"));
    }
}
