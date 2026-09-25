using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Obras;

namespace Facturacion.Server.Modules.Documentos.Obras;

public static class ValidadorFiscalDeObra
{
    public static ErrorNegocio? Validar(Comprobante comprobante, DatosObra obra)
    {
        if (obra.TipoObra is not (TiposDeObra.Publica or TiposDeObra.Privada))
            return ErrorNegocio.Validacion("obra-tipo-invalido",
                "Indica si el contrato es de obra pública o privada.");

        if (comprobante.TipoDeComprobante != "I" || comprobante.Exportacion != "01")
            return ErrorNegocio.Regla("obra-operacion-no-disponible",
                "Esta estimación solo se puede emitir como factura de ingreso sin exportación.");

        if (obra.TipoObra == TiposDeObra.Publica &&
            (obra.NombreDeduccion1 != "5 al millar" || obra.PorcentajeDeduccion1 != 0.5m))
            return ErrorNegocio.Regla("obra-publica-cinco-al-millar-no-configurado",
                "Este flujo de obra pública requiere la retención de 5 al millar a 0.50 %. Si tu contrato tiene otro tratamiento, conserva el borrador para revisión fiscal.");

        if (obra.PorcentajeAmortizacion != 0)
            return ErrorNegocio.Regla("obra-amortizacion-sin-tratamiento-fiscal",
                "La amortización del contrato no se puede restar automáticamente del CFDI. Se necesita identificar el documento del pago previo y definir si fue anticipo fiscal o parcialidad. Conserva el borrador; no se reservará folio ni timbre.");

        if (obra.PorcentajeRetenciones != 0 || obra.PorcentajeDevoluciones != 0 ||
            obra.Retenciones != 0 || obra.Devoluciones != 0)
            return ErrorNegocio.Regla("obra-ajustes-contractuales-sin-clasificar",
                "Las retenciones y devoluciones del contrato no equivalen por sí solas a impuestos o descuentos del CFDI. Falta definir su naturaleza fiscal. Conserva el borrador; no se reservará folio ni timbre.");

        if ((obra.TipoObra == TiposDeObra.Privada && obra.PorcentajeDeduccion1 != 0) ||
            obra.PorcentajeDeduccion2 != 0 || obra.PorcentajeDeduccion3 != 0 ||
            obra.PorcentajeDeduccion4 != 0)
            return ErrorNegocio.Regla("obra-deducciones-sin-clasificar",
                "Las deducciones distintas del 5 al millar público necesitan clasificación fiscal antes de emitir. Conserva el borrador; no se reservará folio ni timbre.");

        if (comprobante.Conceptos.Count == 0)
            return ErrorNegocio.Validacion("obra-sin-trabajos",
                "Guarda primero los conceptos de la factura.");

        if (comprobante.Moneda != "MXN")
            return ErrorNegocio.Regla("obra-moneda-no-disponible",
                "La conciliación fiscal de estimaciones de obra solo está disponible en pesos mexicanos.");

        if (comprobante.Descuento != 0 || comprobante.TotalImpuestosRetenidos != 0)
            return ErrorNegocio.Regla("obra-importes-fiscales-distintos",
                "El cálculo de obra no contempla descuentos ni impuestos retenidos del CFDI. Revisa los conceptos antes de continuar.");

        var impuestos = comprobante.Conceptos.SelectMany(c => c.Impuestos).ToArray();
        if (impuestos.Any(i => i.EsRetencion || i.Impuesto != "002" || i.TipoFactor != "Tasa" ||
            i.TasaOCuota is null || i.TasaOCuota.Value * 100m != obra.PorcentajeIva))
            return ErrorNegocio.Regla("obra-iva-no-coincide",
                "Los conceptos deben tener únicamente IVA trasladado a la misma tasa indicada en la estimación.");

        if (obra.PorcentajeIva != 0 && impuestos.Length == 0)
            return ErrorNegocio.Regla("obra-iva-no-coincide",
                "La estimación indica IVA, pero los conceptos no tienen ese impuesto.");

        if (obra.PorcentajeIva != 0 && comprobante.Conceptos.Any(c => c.Impuestos.Count != 1))
            return ErrorNegocio.Regla("obra-iva-mixto",
                "La estimación usa una sola tasa de IVA. Todos los conceptos deben tener exactamente ese traslado; revisa los renglones sin IVA o con varios impuestos.");

        // El XML redondea cada concepto antes de sumar; comparar solo el total persistido
        // a seis decimales podría aprobar una diferencia de un centavo en el documento final.
        var subtotalFiscal = comprobante.Conceptos.Sum(c => Redondear(c.Importe));
        var ivaFiscal = impuestos.Sum(i => Redondear(i.Importe ?? 0));
        if (subtotalFiscal != Redondear(comprobante.SubTotal) ||
            subtotalFiscal + ivaFiscal != Redondear(comprobante.Total))
            return ErrorNegocio.Regla("obra-total-no-coincide",
                "El total guardado difiere del que saldría en el XML al redondear cada concepto. Revisa los importes de la factura antes de emitir.");

        return null;
    }

    private static decimal Redondear(decimal valor)
        => Math.Round(valor, 2, MidpointRounding.ToEven);
}
