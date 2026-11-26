using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Obras;

namespace Facturacion.Server.Modules.Documentos.Obras;

public static class ValidadorFiscalDeObra
{
    public static ErrorNegocio? Validar(Comprobante comprobante, DatosObra obra)
    {
        if (ServicioParcialConstruccion.Leer(obra.ServicioParcialJson) is { } parcial)
        {
            if (ServicioParcialConstruccion.Validar(parcial) is { } error) return error;
            if (comprobante.TotalImpuestosTrasladados != 0 ||
                comprobante.Conceptos.SelectMany(c => c.Impuestos).Any(i => i.Impuesto == "002" && i.TipoFactor != "Exento"))
                return ErrorNegocio.Validacion("servicio-parcial-iva", "El servicio parcial con estímulo de vivienda requiere IVA exento (sin tasa trasladada). Revisa los conceptos.");
        }

        if (comprobante.TipoDeComprobante != "I" || comprobante.Exportacion != "01")
            return ErrorNegocio.Regla("obra-operacion-no-disponible",
                "Esta estimación solo se puede emitir como factura de ingreso sin exportación.");

        // La primera deducción va al complemento de impuestos locales, que admite dos decimales en la tasa y no permite "|".
        if (obra.PorcentajeDeduccion1 != 0)
        {
            if (decimal.Round(obra.PorcentajeDeduccion1, 2) != obra.PorcentajeDeduccion1)
                return ErrorNegocio.Validacion("obra-retencion-local-decimales",
                    $"El porcentaje de {obra.NombreDeduccion1} admite solo dos decimales.");
            if (obra.NombreDeduccion1.Contains('|'))
                return ErrorNegocio.Validacion("obra-retencion-local-nombre",
                    "El nombre de la deducción no puede contener el carácter «|».");
        }

        // Si se relacionan anticipos con tipo 07, solo puede haber uno por factura.
        if (obra.PorcentajeAmortizacion != 0 &&
            comprobante.Relacionados.Count(x => x.TipoRelacion == AmortizacionDeObra.TipoRelacionAnticipo) > 1)
            return ErrorNegocio.Regla("obra-amortizacion-varios-anticipos",
                "Solo puedes relacionar un anticipo con tipo 07 por factura de obra.");

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

        // El XML proyecta cada concepto antes de sumar; usamos exactamente la misma proyección
        // para que nunca haya diferencias de un centavo entre el borrador y el CFDI final.
        var proyeccion = ProyeccionMonetariaCfdi.Calcular(comprobante, 2, obra);
        if (proyeccion.Conceptos.Any(c => c.Impuestos.Length > 0 && c.Importe - c.Descuento <= 0))
            return ErrorNegocio.Validacion("obra-base-cero",
                "Un concepto con impuestos no puede quedar con base cero tras la amortización (CFDI40176).");

        if (proyeccion.Total < 0)
            return ErrorNegocio.Validacion("obra-total-negativo",
                "El total fiscal del CFDI no puede ser negativo.");
        if (!proyeccion.CoincideCon(comprobante))
            return ErrorNegocio.Regla("obra-total-no-coincide",
                "El total guardado difiere del que saldría en el XML al redondear cada concepto. Revisa los importes de la factura antes de emitir.");

        return null;
    }
}
