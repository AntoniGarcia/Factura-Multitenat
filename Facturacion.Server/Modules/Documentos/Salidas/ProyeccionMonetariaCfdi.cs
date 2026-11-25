using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Impuestos;
using Facturacion.Server.Modules.Documentos.Obras;

namespace Facturacion.Server.Modules.Documentos.Salidas;

internal sealed record ImpuestoMonetarioCfdi(ImpuestoConcepto Impuesto, decimal Base, decimal? Importe);

internal sealed record ConceptoMonetarioCfdi(
    Concepto Concepto, decimal Importe, decimal Descuento, ImpuestoMonetarioCfdi[] Impuestos);

// Importe nulo en el grupo exento: el SAT lo resume solo con base, impuesto y factor.
internal sealed record TrasladoMonetarioCfdi(
    string Impuesto, string TipoFactor, decimal? TasaOCuota, decimal Base, decimal? Importe);

internal sealed record RetencionMonetariaCfdi(string Impuesto, decimal Importe);

// Tasa en porcentaje, como TasadeTraslado y TasadeRetencion: 3.00 es el 3 %.
public sealed record ImpuestoLocalCfdi(string Nombre, decimal Tasa, decimal Importe, bool EsRetencion);

// Locales: fuera de los totales federales pero dentro del Total, como pide el Anexo 20.
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
    // El complemento de impuestos locales lleva dos decimales en tasas e importes.
    public const int DecimalesLocales = 2;

    // obra: solo en factura de obra; omitirla ahí cambia el Total.
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
            .Where(x => !x.Impuesto.EsRetencion)
            .GroupBy(x => (x.Impuesto.Impuesto, x.Impuesto.TipoFactor, x.Impuesto.TasaOCuota))
            .Select(grupo => new TrasladoMonetarioCfdi(
                grupo.Key.Impuesto,
                grupo.Key.TipoFactor,
                grupo.Key.TasaOCuota,
                grupo.Sum(x => x.Base),
                grupo.Any(x => x.Importe is not null) ? grupo.Sum(x => x.Importe ?? 0m) : null))
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
        var totalTrasladados = traslados.Sum(x => x.Importe ?? 0m);
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

    // Es la única forma en que se escriben los totales del comprobante.
    public void AplicarA(Comprobante comprobante)
    {
        comprobante.SubTotal = SubTotal;
        comprobante.Descuento = Descuento;
        comprobante.TotalImpuestosTrasladados = TotalImpuestosTrasladados;
        comprobante.TotalImpuestosRetenidos = TotalImpuestosRetenidos;
        comprobante.Total = Total;
    }
}
