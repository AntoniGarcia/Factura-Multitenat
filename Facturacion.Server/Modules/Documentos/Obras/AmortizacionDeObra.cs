using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Impuestos;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.Obras;

/// Amortización del anticipo por remanente (Apéndice 6 del Anexo 20): relaciona el anticipo
/// con tipo 07, cada concepto lleva su parte como descuento y el IVA se calcula sobre el resto.
/// Así subtotal, IVA y total de la estimación son los del CFDI.
public static class AmortizacionDeObra
{
    public const string Leyenda = "CFDI por remanente de un anticipo";
    public const string TipoRelacionAnticipo = "07";

    private const string Separador = " — ";
    private const int LargoMaximoDeDescripcion = 1000;

    // Renglón por renglón: la suma de descuentos es la amortización exacta del XML.
    public static decimal DescuentoDe(decimal importe, decimal porcentaje)
        => Math.Round(importe * porcentaje / 100m, MotorDeImpuestos.Decimales, MotorDeImpuestos.ModoDeRedondeo);

    public static bool TieneLeyenda(string descripcion)
        => descripcion.EndsWith(Separador + Leyenda, StringComparison.Ordinal);

    // Sin estimación guardada, o con 0 %, deja los conceptos sin descuento ni leyenda.
    public static ErrorNegocio? Aplicar(Comprobante comprobante, DatosObra? obra, int decimalesMoneda)
    {
        var porcentaje = obra?.PorcentajeAmortizacion ?? 0m;
        var llevaLeyenda = porcentaje != 0 && comprobante.Relacionados.Any(x => x.TipoRelacion == TipoRelacionAnticipo);
        var conceptos = comprobante.Conceptos.OrderBy(x => x.Orden).ToArray();
        for (var i = 0; i < conceptos.Length; i++)
        {
            var descripcion = TieneLeyenda(conceptos[i].Descripcion)
                ? conceptos[i].Descripcion[..^(Separador.Length + Leyenda.Length)]
                : conceptos[i].Descripcion;
            if (llevaLeyenda) descripcion += Separador + Leyenda;
            if (descripcion.Length > LargoMaximoDeDescripcion)
                return ErrorNegocio.Validacion("obra-leyenda-anticipo",
                    $"La descripción del renglón {i + 1} no deja espacio para la leyenda «{Leyenda}». " +
                    $"Acórtala a {LargoMaximoDeDescripcion - Separador.Length - Leyenda.Length} caracteres.");
            conceptos[i].Descripcion = descripcion;
        }

        var impuestos = conceptos.Select(x => x.Impuestos.ToArray()).ToArray();
        var calculado = MotorDeImpuestos.Calcular(new ComprobanteACalcular(comprobante.Moneda, comprobante.TipoCambio,
            [.. conceptos.Select((concepto, i) => new ConceptoACalcular(concepto.Cantidad, concepto.ValorUnitario,
                DescuentoDe(concepto.Importe, porcentaje), concepto.ObjetoImp,
                [.. impuestos[i].Select(x => new ImpuestoDeConcepto(x.Impuesto, x.TipoFactor, x.TasaOCuota, x.EsRetencion))]))]));
        if (calculado.EsFallo) return calculado.Error!;

        for (var i = 0; i < conceptos.Length; i++)
        {
            var calculo = calculado.Valor.Conceptos[i];
            conceptos[i].Importe = calculo.Importe;
            conceptos[i].Descuento = calculo.Descuento;
            for (var j = 0; j < impuestos[i].Length; j++)
            {
                impuestos[i][j].Base = calculo.Impuestos[j].Base;
                impuestos[i][j].Importe = calculo.Impuestos[j].Importe;
            }
        }

        ProyeccionMonetariaCfdi.Calcular(comprobante, decimalesMoneda, obra).AplicarA(comprobante);
        return null;

    }

    // ¿Los conceptos guardados reflejan el porcentaje de amortización?
    public static bool EstaAplicada(Comprobante comprobante, decimal porcentaje)
    {
        var llevaLeyenda = porcentaje != 0 && comprobante.Relacionados.Any(x => x.TipoRelacion == TipoRelacionAnticipo);
        return comprobante.Conceptos.All(x =>
            x.Descuento == DescuentoDe(x.Importe, porcentaje) && TieneLeyenda(x.Descripcion) == llevaLeyenda);
    }
}
