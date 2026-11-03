using Facturacion.Server.Modules.Documentos.Salidas;

namespace Facturacion.Server.Modules.Documentos.Impuestos;

/// <summary>
/// Impuesto estatal sobre hospedaje. Viaja como traslado en el complemento de impuestos
/// locales y suma al Total, pero no entra a la base del IVA (art. 41, fr. I, LIVA): por eso se
/// calcula aparte del motor, sobre el importe menos el descuento de los conceptos que lo aplican.
/// </summary>
public static class ImpuestoSobreHospedaje
{
    public const string Nombre = "ISH";

    /// <param name="tasa">Fracción: 0.03 es el 3 %.</param>
    /// <param name="baseGravable">Suma de los conceptos marcados, ya en los decimales de la moneda.</param>
    public static ImpuestoLocalCfdi? Calcular(decimal? tasa, decimal baseGravable)
        => tasa is { } t && t > 0 && baseGravable > 0
            ? new(Nombre, t * 100m,
                Math.Round(baseGravable * t, ProyeccionMonetariaCfdi.DecimalesLocales, MidpointRounding.ToEven),
                EsRetencion: false)
            : null;

    /// <summary>El complemento admite la tasa en porcentaje con dos decimales.</summary>
    public static bool EsTasaValida(decimal tasa)
        => tasa is > 0 and <= 1 && decimal.Round(tasa * 100m, ProyeccionMonetariaCfdi.DecimalesLocales) == tasa * 100m;
}
