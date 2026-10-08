namespace Facturacion.Shared.Comun;

/// <summary>
/// Regla del atributo <c>Unidad</c> del concepto en CFDI 4.0: de 1 a 20 caracteres y sin
/// <c>|</c> (patrón <c>[^|]{1,20}</c> de <c>cfdv40.xsd</c>). Vive en Shared porque la aplican el
/// alta de producto, la importación CSV y la emisión, y el <c>Client</c> la usa para no precargar
/// un nombre del catálogo que no cabe.
/// </summary>
public static class UnidadDeConcepto
{
    public const int LongitudMaxima = 20;

    public static bool EsValida(string? unidad)
        => !string.IsNullOrWhiteSpace(unidad) &&
           unidad.Trim().Length <= LongitudMaxima &&
           !unidad.Contains('|');

    public const string Regla = "La unidad de medida admite hasta 20 caracteres y no puede contener «|».";
}
