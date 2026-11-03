using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Impuestos;
using Facturacion.Server.Modules.Documentos.Obras;

namespace Facturacion.Server.Modules.Documentos.Salidas;

internal sealed record ImpuestoMonetarioCfdi(ImpuestoConcepto Impuesto, decimal Base, decimal? Importe);

internal sealed record ConceptoMonetarioCfdi(
    Concepto Concepto, decimal Importe, decimal Descuento, ImpuestoMonetarioCfdi[] Impuestos);

internal sealed record TrasladoMonetarioCfdi(
    string Impuesto, string TipoFactor, decimal? TasaOCuota, decimal Base, decimal Importe);

internal sealed record RetencionMonetariaCfdi(string Impuesto, decimal Importe);

/// <summary>Un renglón del complemento de impuestos locales.</summary>
/// <param name="Tasa">
/// Porcentaje, como lo piden <c>TasadeTraslado</c> y <c>TasadeRetencion</c>: 3.00 es el 3 %.
/// </param>
public sealed record ImpuestoLocalCfdi(string Nombre, decimal Tasa, decimal Importe, bool EsRetencion);

/// <param name="Locales">
/// Fuera de los totales federales, pero dentro del <see cref="Total"/>: el Anexo 20 pide
/// <c>Total = SubTotal − Descuento + traslados − retenciones</c>, federales y locales.
/// </param>
internal sealed record ProyeccionMonetariaCfdi(
    ConceptoMonetarioCfdi[] Conceptos,
    TrasladoMonetarioCfdi[] Traslados,
    RetencionMonetariaCfdi[] Retenciones,
    ImpuestoLocalCfdi[] Locales,
    decimal SubTotal,
    decimal Descuento,
    decimal TotalImpuestosTrasladados,
    decimal TotalImpuestosRetenidos,
    decimal Total)
{
    /// <summary>El complemento de impuestos locales lleva dos decimales en tasas e importes.</summary>
    public const int DecimalesLocales = 2;

    /// <param name="obra">
    /// La estimación de una factura de obra, que aporta su retención local. Nula en cualquier
    /// otra factura; quien llama no puede omitirla en una de obra sin cambiar el Total.
    /// </param>
    public static ProyeccionMonetariaCfdi Calcular(Comprobante comprobante, int decimales, DatosObra? obra)
    {
        decimal Redondear(decimal valor)
            => Math.Round(valor, decimales, MotorDeImpuestos.ModoDeRedondeo);

        var conceptos = comprobante.Conceptos
            .OrderBy(x => x.Orden)
            .Select(x => new ConceptoMonetarioCfdi(
                x,
                Redondear(x.Importe),
                Redondear(x.Descuento),
                [.. x.Impuestos.Select(i => new ImpuestoMonetarioCfdi(
                    i, Redondear(i.Base), i.Importe is { } importe ? Redondear(importe) : null))]))
            .ToArray();

        var impuestos = conceptos.SelectMany(x => x.Impuestos).ToArray();
        var traslados = impuestos
            .Where(x => !x.Impuesto.EsRetencion && x.Importe is not null)
            .GroupBy(x => (x.Impuesto.Impuesto, x.Impuesto.TipoFactor, x.Impuesto.TasaOCuota))
            .Select(grupo => new TrasladoMonetarioCfdi(
                grupo.Key.Impuesto,
                grupo.Key.TipoFactor,
                grupo.Key.TasaOCuota,
                grupo.Sum(x => x.Base),
                grupo.Sum(x => x.Importe!.Value)))
            .OrderBy(x => x.Impuesto).ThenBy(x => x.TasaOCuota)
            .ToArray();
        var retenciones = impuestos
            .Where(x => x.Impuesto.EsRetencion && x.Importe is not null)
            .GroupBy(x => x.Impuesto.Impuesto)
            .Select(grupo => new RetencionMonetariaCfdi(
                grupo.Key, grupo.Sum(x => x.Importe!.Value)))
            .OrderBy(x => x.Impuesto)
            .ToArray();

        var subtotal = conceptos.Sum(x => x.Importe);
        var descuento = conceptos.Sum(x => x.Descuento);
        var totalTrasladados = traslados.Sum(x => x.Importe);
        var totalRetenidos = retenciones.Sum(x => x.Importe);

        var locales = new List<ImpuestoLocalCfdi>();
        if (RetencionLocalDeObra.Calcular(obra, subtotal) is { } retencionDeObra)
            locales.Add(retencionDeObra);
        if (ImpuestoSobreHospedaje.Calcular(comprobante.TasaIsh,
                conceptos.Where(x => x.Concepto.AplicaIsh).Sum(x => x.Importe - x.Descuento)) is { } ish)
            locales.Add(ish);
        var trasladosLocales = locales.Where(x => !x.EsRetencion).Sum(x => x.Importe);
        var retencionesLocales = locales.Where(x => x.EsRetencion).Sum(x => x.Importe);

        return new ProyeccionMonetariaCfdi(conceptos, traslados, retenciones, [.. locales],
            subtotal, descuento, totalTrasladados, totalRetenidos,
            subtotal - descuento + totalTrasladados - totalRetenidos + trasladosLocales - retencionesLocales);
    }

    public bool CoincideCon(Comprobante comprobante)
        => comprobante.SubTotal == SubTotal && comprobante.Descuento == Descuento &&
           comprobante.TotalImpuestosTrasladados == TotalImpuestosTrasladados &&
           comprobante.TotalImpuestosRetenidos == TotalImpuestosRetenidos &&
           comprobante.Total == Total;

    /// <summary>Copia los totales al comprobante. Es la única forma en que se escriben.</summary>
    public void AplicarA(Comprobante comprobante)
    {
        comprobante.SubTotal = SubTotal;
        comprobante.Descuento = Descuento;
        comprobante.TotalImpuestosTrasladados = TotalImpuestosTrasladados;
        comprobante.TotalImpuestosRetenidos = TotalImpuestosRetenidos;
        comprobante.Total = Total;
    }
}
