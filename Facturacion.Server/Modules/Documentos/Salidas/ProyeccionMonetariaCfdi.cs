using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Impuestos;

namespace Facturacion.Server.Modules.Documentos.Salidas;

internal sealed record ImpuestoMonetarioCfdi(ImpuestoConcepto Impuesto, decimal Base, decimal? Importe);

internal sealed record ConceptoMonetarioCfdi(
    Concepto Concepto, decimal Importe, decimal Descuento, ImpuestoMonetarioCfdi[] Impuestos);

internal sealed record TrasladoMonetarioCfdi(
    string Impuesto, string TipoFactor, decimal? TasaOCuota, decimal Base, decimal Importe);

internal sealed record RetencionMonetariaCfdi(string Impuesto, decimal Importe);

internal sealed record ProyeccionMonetariaCfdi(
    ConceptoMonetarioCfdi[] Conceptos,
    TrasladoMonetarioCfdi[] Traslados,
    RetencionMonetariaCfdi[] Retenciones,
    decimal SubTotal,
    decimal Descuento,
    decimal TotalImpuestosTrasladados,
    decimal TotalImpuestosRetenidos,
    decimal Total)
{
    public static ProyeccionMonetariaCfdi Calcular(Comprobante comprobante, int decimales)
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
        return new ProyeccionMonetariaCfdi(conceptos, traslados, retenciones,
            subtotal, descuento, totalTrasladados, totalRetenidos,
            subtotal - descuento + totalTrasladados - totalRetenidos);
    }

    public bool CoincideCon(Comprobante comprobante)
        => comprobante.SubTotal == SubTotal && comprobante.Descuento == Descuento &&
           comprobante.TotalImpuestosTrasladados == TotalImpuestosTrasladados &&
           comprobante.TotalImpuestosRetenidos == TotalImpuestosRetenidos &&
           comprobante.Total == Total;
}
