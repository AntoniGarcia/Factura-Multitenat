using Facturacion.Server.Modules.Documentos.Salidas;

namespace Facturacion.Server.Modules.Documentos.Impuestos;

/// Impuesto estatal sobre hospedaje: va en el complemento de impuestos locales y suma al Total,
/// pero no a la base del IVA (art. 41, fr. I, LIVA). Por eso se calcula aparte del motor,
/// sobre importe menos descuento de los conceptos que lo aplican.
public static class ImpuestoSobreHospedaje
{
    public const string Nombre = "ISH";

    // tasa: fracción (0.03 es el 3 %). baseGravable: ya en los decimales de la moneda.
    public static ImpuestoLocalCfdi? Calcular(decimal? tasa, decimal baseGravable)
        => tasa is { } t && t > 0 && baseGravable > 0
            ? new(Nombre, t * 100m,
                Math.Round(baseGravable * t, ProyeccionMonetariaCfdi.DecimalesLocales, MidpointRounding.ToEven),
                EsRetencion: false)
            : null;

    // El complemento admite la tasa en porcentaje con dos decimales.
    public static bool EsTasaValida(decimal tasa)
        => tasa is > 0 and <= 1 && decimal.Round(tasa * 100m, ProyeccionMonetariaCfdi.DecimalesLocales) == tasa * 100m;
}
