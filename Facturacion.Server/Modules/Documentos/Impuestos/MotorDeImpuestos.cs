using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.Impuestos;

/// <summary>
/// Calcula importes, impuestos y totales de un comprobante. Es la pieza más cara de
/// equivocar del sistema, así que es <b>pura</b>: no toca base de datos, ni red, ni reloj.
/// Todo lo que necesita entra por parámetro y todo lo que decide sale de vuelta, que es lo
/// que permite cubrirla con decenas de casos sin montar nada.
///
/// <para><b>La regla que evita el rechazo más común del PAC</b></para>
/// Los totales del comprobante son la <b>suma de los impuestos ya redondeados de cada
/// concepto</b>, nunca el impuesto calculado sobre el total. Las dos cuentas no dan lo mismo
/// —cada redondeo por renglón arrastra su propio residuo— y el PAC valida la primera. Aquí
/// no hay ni una línea que aplique una tasa a un total.
///
/// <para><b>Exento no es tasa cero</b></para>
/// Un exento no lleva importe: en el XML es un nodo sin ese atributo. Una tasa cero sí lo
/// lleva, valiendo cero. Por eso <see cref="ImpuestoCalculado.Importe"/> es anulable y no un
/// <c>decimal</c> con cero: si fueran lo mismo, el XML saldría mal y el rechazo llegaría
/// hasta el timbrado.
/// </summary>
public static class MotorDeImpuestos
{
    /// <summary>Decimales de cálculo. La presentación redondea a dos (CLAUDE.md §5).</summary>
    public const int Decimales = 6;

    /// <summary>
    /// Modo de redondeo en el punto medio.
    ///
    /// <para>
    /// Lo fija el prompt de la fase B1 como redondeo bancario. <b>No se pudo confirmar contra
    /// el SAT</b>: el Anexo 20 define de cero a seis decimales y prohíbe importes de impuesto
    /// negativos, pero no encontré que mande un modo concreto para el punto medio. Está aquí,
    /// en una sola constante, para que cambiarlo sea una línea; hay una prueba que lo fija, y
    /// cambiarlo la hace fallar a propósito.
    /// </para>
    /// </summary>
    public const MidpointRounding ModoDeRedondeo = MidpointRounding.ToEven;

    private const string Mxn = "MXN";

    public static Resultado<ComprobanteCalculado> Calcular(ComprobanteACalcular comprobante)
    {
        if (Validar(comprobante) is { } error) return error;

        var conceptos = comprobante.Conceptos.Select(Calcular).ToArray();

        var subTotal = Redondear(conceptos.Sum(c => c.Importe));
        var descuento = Redondear(conceptos.Sum(c => c.Descuento));

        var traslados = Agrupar(conceptos, retenciones: false);
        var retenciones = Agrupar(conceptos, retenciones: true);

        // Los totales salen de los renglones ya agrupados, que a su vez salen de los importes
        // ya redondeados de cada concepto. En ningún punto se recalcula sobre el total.
        var totalTrasladados = Redondear(traslados.Sum(t => t.Importe));
        var totalRetenidos = Redondear(retenciones.Sum(r => r.Importe));

        var total = Redondear(subTotal - descuento + totalTrasladados - totalRetenidos);

        return new ComprobanteCalculado(
            subTotal, descuento, totalTrasladados, totalRetenidos, total,
            conceptos, traslados, retenciones);
    }

    private static ConceptoCalculado Calcular(ConceptoACalcular concepto)
    {
        var importe = Redondear(concepto.Cantidad * concepto.ValorUnitario);
        var descuento = Redondear(concepto.Descuento);
        var baseGravable = Redondear(importe - descuento);

        var impuestos = concepto.Impuestos
            .Select(i => new ImpuestoCalculado(
                i.Impuesto,
                i.TipoFactor,
                i.TasaOCuota,
                baseGravable,
                // El exento se queda sin importe a propósito; ver el resumen de la clase.
                i.TipoFactor == TiposDeFactor.Exento
                    ? null
                    : Redondear(baseGravable * i.TasaOCuota!.Value),
                i.EsRetencion))
            .ToArray();

        return new ConceptoCalculado(importe, descuento, baseGravable, impuestos);
    }

    /// <summary>
    /// Junta los impuestos de todos los conceptos para el nodo <c>Impuestos</c> del
    /// comprobante. Los traslados se agrupan por impuesto, tipo de factor y tasa —dos IVA a
    /// tasas distintas son dos renglones—; las retenciones, por impuesto y tipo de factor,
    /// que es como las pide el estándar.
    /// </summary>
    private static ImpuestoAgrupado[] Agrupar(IEnumerable<ConceptoCalculado> conceptos, bool retenciones)
        => [.. conceptos
            .SelectMany(c => c.Impuestos)
            .Where(i => i.EsRetencion == retenciones)
            // Sin importe no hay renglón que agrupar: un exento no cabe en este nodo.
            .Where(i => i.Importe is not null)
            .GroupBy(i => retenciones
                ? (i.Impuesto, i.TipoFactor, TasaOCuota: (decimal?)null)
                : (i.Impuesto, i.TipoFactor, i.TasaOCuota))
            .Select(g => new ImpuestoAgrupado(
                g.Key.Impuesto,
                g.Key.TipoFactor,
                g.Key.TasaOCuota,
                Redondear(g.Sum(i => i.Base)),
                Redondear(g.Sum(i => i.Importe!.Value))))
            .OrderBy(g => g.Impuesto)
            .ThenBy(g => g.TasaOCuota)];

    private static ErrorNegocio? Validar(ComprobanteACalcular comprobante)
    {
        if (comprobante.Conceptos.Count == 0)
            return ErrorNegocio.Validacion("sin-conceptos", "El comprobante necesita al menos un concepto.");

        if (string.IsNullOrWhiteSpace(comprobante.Moneda))
            return ErrorNegocio.Validacion("sin-moneda", "El comprobante necesita una moneda.");

        // El tipo de cambio no convierte nada: solo se declara. Pero sin él, un comprobante
        // en dólares no dice cuánto valían esos dólares ese día, y el SAT lo rechaza.
        if (comprobante.Moneda != Mxn && comprobante.TipoCambio is not > 0)
            return ErrorNegocio.Validacion(
                "sin-tipo-de-cambio",
                $"Con moneda {comprobante.Moneda} el tipo de cambio es obligatorio y debe ser mayor que cero.");

        if (comprobante.Moneda == Mxn && comprobante.TipoCambio is { } cambio && cambio != 1m)
            return ErrorNegocio.Validacion(
                "tipo-de-cambio-en-pesos",
                "Con moneda MXN el tipo de cambio se omite, o vale exactamente 1.");

        for (var i = 0; i < comprobante.Conceptos.Count; i++)
        {
            if (Validar(comprobante.Conceptos[i], i + 1) is { } error) return error;
        }

        return null;
    }

    private static ErrorNegocio? Validar(ConceptoACalcular concepto, int renglon)
    {
        if (concepto.Cantidad <= 0)
            return ErrorNegocio.Validacion("cantidad-invalida", $"La cantidad del renglón {renglon} debe ser mayor que cero.");

        if (concepto.ValorUnitario < 0)
            return ErrorNegocio.Validacion("valor-unitario-invalido", $"El valor unitario del renglón {renglon} no puede ser negativo.");

        if (concepto.Descuento < 0)
            return ErrorNegocio.Validacion("descuento-invalido", $"El descuento del renglón {renglon} no puede ser negativo.");

        if (Redondear(concepto.Descuento) > Redondear(concepto.Cantidad * concepto.ValorUnitario))
            return ErrorNegocio.Validacion(
                "descuento-mayor-que-importe",
                $"El descuento del renglón {renglon} no puede superar su importe.");

        // Un renglón no objeto de impuesto que trae impuestos es una contradicción, y produce
        // un XML que el SAT rechaza.
        if (concepto.ObjetoImp == ObjetosDeImpuesto.NoObjeto && concepto.Impuestos.Count > 0)
            return ErrorNegocio.Validacion(
                "no-objeto-con-impuestos",
                $"El renglón {renglon} está marcado como no objeto de impuesto y trae impuestos.");

        if (concepto.ObjetoImp == ObjetosDeImpuesto.SiObjeto && concepto.Impuestos.Count == 0)
            return ErrorNegocio.Validacion(
                "si-objeto-sin-impuestos",
                $"El renglón {renglon} está marcado como objeto de impuesto y no trae ninguno.");

        foreach (var impuesto in concepto.Impuestos)
        {
            if (impuesto.TipoFactor == TiposDeFactor.Exento)
            {
                if (impuesto.TasaOCuota is not null)
                    return ErrorNegocio.Validacion(
                        "exento-con-tasa",
                        $"El impuesto {impuesto.Impuesto} del renglón {renglon} es exento y no lleva tasa. " +
                        "Una tasa de cero no es lo mismo que exento.");

                // El SAT no admite retenciones exentas: una retención siempre tiene tasa.
                if (impuesto.EsRetencion)
                    return ErrorNegocio.Validacion(
                        "retencion-exenta",
                        $"La retención de {impuesto.Impuesto} del renglón {renglon} no puede ser exenta.");

                continue;
            }

            if (impuesto.TasaOCuota is not { } tasa)
                return ErrorNegocio.Validacion(
                    "tasa-faltante",
                    $"El impuesto {impuesto.Impuesto} del renglón {renglon} es de tipo {impuesto.TipoFactor} y necesita tasa o cuota.");

            if (tasa < 0)
                return ErrorNegocio.Validacion(
                    "tasa-negativa",
                    $"La tasa del impuesto {impuesto.Impuesto} del renglón {renglon} no puede ser negativa.");
        }

        var repetido = concepto.Impuestos
            .GroupBy(i => (i.Impuesto, i.EsRetencion))
            .FirstOrDefault(g => g.Count() > 1);

        if (repetido is not null)
            return ErrorNegocio.Validacion(
                "impuesto-repetido",
                $"El renglón {renglon} declara dos veces el impuesto {repetido.Key.Impuesto} en el mismo sentido.");

        return null;
    }

    private static decimal Redondear(decimal valor) => Math.Round(valor, Decimales, ModoDeRedondeo);
}
