namespace Facturacion.Shared.Comun;

// Unidad del concepto en CFDI 4.0: de 1 a 20 caracteres y sin «|» (cfdv40.xsd).
public static class UnidadDeConcepto
{
    public const int LongitudMaxima = 20;

    public static bool EsValida(string? unidad)
        => !string.IsNullOrWhiteSpace(unidad) &&
           unidad.Trim().Length <= LongitudMaxima &&
           !unidad.Contains('|');

    public const string Regla = "La unidad de medida admite hasta 20 caracteres y no puede contener «|».";
}
