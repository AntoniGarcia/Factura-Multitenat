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

        // La primera deducción de obra pública viaja al complemento de impuestos locales, cuya
        // tasa admite dos decimales; las demás deducciones solo restan del importe líquido.
        if (obra.TipoObra == TiposDeObra.Publica &&
            decimal.Round(obra.PorcentajeDeduccion1, 2) != obra.PorcentajeDeduccion1)
            return ErrorNegocio.Validacion("obra-retencion-local-decimales",
                $"El porcentaje de {obra.NombreDeduccion1} admite solo dos decimales.");

        // Retenciones y devoluciones del contrato se restan del pago, después del total, y no
        // tocan el CFDI. La amortización sí: va como descuento de los conceptos (procedimiento de
        // remanente), así que exige el anticipo relacionado; el saldo se revisa contra la base.
        if (obra.PorcentajeAmortizacion != 0 &&
            comprobante.Relacionados.Count(x => x.TipoRelacion == AmortizacionDeObra.TipoRelacionAnticipo) != 1)
            return ErrorNegocio.Regla("obra-amortizacion-sin-anticipo",
                "Para amortizar, relaciona en «CFDI relacionados» un solo anticipo con tipo 07 o deja la amortización en 0.");

        if (comprobante.Conceptos.Count == 0)
            return ErrorNegocio.Validacion("obra-sin-trabajos",
                "Guarda primero los conceptos de la factura.");

        if (comprobante.Moneda != "MXN")
            return ErrorNegocio.Regla("obra-moneda-no-disponible",
                "La conciliación fiscal de estimaciones de obra solo está disponible en pesos mexicanos.");

        if (!AmortizacionDeObra.EstaAplicada(comprobante, obra.PorcentajeAmortizacion))
            return ErrorNegocio.Regla("obra-amortizacion-desactualizada",
                "Los descuentos de los conceptos no corresponden a la amortización de la pestaña Obra. Guarda el cálculo de obra de nuevo.");

        if (comprobante.TotalImpuestosRetenidos != 0)
            return ErrorNegocio.Regla("obra-importes-fiscales-distintos",
                "El cálculo de obra no contempla impuestos retenidos del CFDI. Revisa los conceptos antes de continuar.");

        // Se admiten tasas mixtas y exentos: la estimación toma el IVA de cada concepto.
        var impuestos = comprobante.Conceptos.SelectMany(c => c.Impuestos).ToArray();
        if (impuestos.Any(i => i.EsRetencion || i.Impuesto != "002" ||
            !(i.TipoFactor == "Exento" || (i.TipoFactor == "Tasa" && i.TasaOCuota is not null))))
            return ErrorNegocio.Regla("obra-solo-iva",
                "Los conceptos de una factura de obra solo pueden llevar IVA trasladado, por tasa o exento.");

        // El XML redondea cada concepto antes de sumar; comparar solo el total persistido
        // a seis decimales podría aprobar una diferencia de un centavo en el documento final.
        var subtotalFiscal = comprobante.Conceptos.Sum(c => Redondear(c.Importe));
        var descuentoFiscal = comprobante.Conceptos.Sum(c => Redondear(c.Descuento));
        var ivaFiscal = impuestos.Sum(i => Redondear(i.Importe ?? 0));
        if (subtotalFiscal != Redondear(comprobante.SubTotal) ||
            subtotalFiscal - descuentoFiscal + ivaFiscal != Redondear(comprobante.Total))
            return ErrorNegocio.Regla("obra-total-no-coincide",
                "El total guardado difiere del que saldría en el XML al redondear cada concepto. Revisa los importes de la factura antes de emitir.");

        return null;
    }

    private static decimal Redondear(decimal valor)
        => Math.Round(valor, 2, MidpointRounding.ToEven);
}
