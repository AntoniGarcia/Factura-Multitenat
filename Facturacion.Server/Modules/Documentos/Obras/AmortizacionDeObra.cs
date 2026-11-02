using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Impuestos;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.Obras;

/// <summary>
/// Amortización del anticipo por el procedimiento de remanente del Apéndice 6 del Anexo 20: la
/// factura relaciona el anticipo con tipo 07, cada concepto lleva como descuento su parte del
/// anticipo y la leyenda «CFDI por remanente de un anticipo», y el IVA se calcula sobre lo que
/// queda. Así el subtotal, el IVA y el total de la estimación son los del CFDI.
/// </summary>
public static class AmortizacionDeObra
{
    public const string Leyenda = "CFDI por remanente de un anticipo";
    public const string TipoRelacionAnticipo = "07";

    private const string Separador = " — ";
    private const int LargoMaximoDeDescripcion = 1000;

    /// <summary>
    /// El porcentaje se aplica renglón por renglón, no sobre el subtotal: lo que suman los
    /// descuentos es la amortización exacta que lleva el XML.
    /// </summary>
    public static decimal DescuentoDe(decimal importe, decimal porcentaje)
        => Math.Round(importe * porcentaje / 100m, MotorDeImpuestos.Decimales, MotorDeImpuestos.ModoDeRedondeo);

    public static bool TieneLeyenda(string descripcion)
        => descripcion.EndsWith(Separador + Leyenda, StringComparison.Ordinal);

    /// <summary>
    /// Reescribe descuento, leyenda e impuestos de cada concepto con el porcentaje dado y
    /// recalcula los totales. Con 0 % deja los conceptos sin descuento y sin leyenda.
    /// </summary>
    public static ErrorNegocio? Aplicar(Comprobante comprobante, decimal porcentaje, int decimalesMoneda)
    {
        var conceptos = comprobante.Conceptos.OrderBy(x => x.Orden).ToArray();
        for (var i = 0; i < conceptos.Length; i++)
        {
            var descripcion = TieneLeyenda(conceptos[i].Descripcion)
                ? conceptos[i].Descripcion[..^(Separador.Length + Leyenda.Length)]
                : conceptos[i].Descripcion;
            if (porcentaje != 0) descripcion += Separador + Leyenda;
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

        var proyeccion = ProyeccionMonetariaCfdi.Calcular(comprobante, decimalesMoneda);
        comprobante.SubTotal = proyeccion.SubTotal;
        comprobante.Descuento = proyeccion.Descuento;
        comprobante.TotalImpuestosTrasladados = proyeccion.TotalImpuestosTrasladados;
        comprobante.TotalImpuestosRetenidos = proyeccion.TotalImpuestosRetenidos;
        comprobante.Total = proyeccion.Total;
        return null;
    }

    /// <summary>Los conceptos guardados ¿reflejan el porcentaje de amortización de la obra?</summary>
    public static bool EstaAplicada(Comprobante comprobante, decimal porcentaje)
        => comprobante.Conceptos.All(x =>
            x.Descuento == DescuentoDe(x.Importe, porcentaje) && TieneLeyenda(x.Descripcion) == (porcentaje != 0));
}
