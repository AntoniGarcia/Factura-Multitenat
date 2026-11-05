using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Impuestos;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.Egresos;

public static class FacturaNetaParaPago
{
    public static Resultado<Comprobante> Calcular(Comprobante origen, IReadOnlyList<Comprobante> notas)
    {
        if (CalculoDeNotaCredito.ValidarOrigen(origen) is { } error) return error;
        var originales = origen.Conceptos.ToDictionary(c => c.Orden);
        foreach (var nota in notas)
        {
            if (nota.TipoDeComprobante != "E" || nota.Variante != CalculoDeNotaCredito.Variante ||
                nota.MetodoPago != "PUE" || nota.ReceptorUsoCfdi != "G02" || nota.TasaIsh is not null ||
                nota.Moneda != origen.Moneda || nota.EmisorRfc != origen.EmisorRfc || nota.ReceptorRfc != origen.ReceptorRfc ||
                nota.Relacionados.Count != 1 || nota.Relacionados[0].UuidRelacionado != origen.Uuid ||
                nota.Relacionados[0].TipoRelacion is not ("01" or "03") || nota.Total <= 0 ||
                nota.Conceptos.Count == 0 || nota.Conceptos.Select(c => c.Orden).Distinct().Count() != nota.Conceptos.Count ||
                !ProyeccionMonetariaCfdi.Calcular(nota, 2, null).CoincideCon(nota))
                return Inconsistente();
            foreach (var c in nota.Conceptos)
            {
                if (!originales.TryGetValue(c.Orden, out var original) || c.Importe <= 0 || c.Descuento != 0 ||
                    c.Cantidad != 1 || c.ValorUnitario != c.Importe ||
                    c.ObjetoImp != original.ObjetoImp || c.Impuestos.Count != original.Impuestos.Count)
                    return Inconsistente();
                foreach (var i in c.Impuestos)
                    if (i.EsRetencion || i.Base != c.Importe || !original.Impuestos.Any(o =>
                        o.Impuesto == i.Impuesto && o.TipoFactor == i.TipoFactor && o.TasaOCuota == i.TasaOCuota) ||
                        (i.TipoFactor == "Exento" ? i.Importe is not null : i.Importe !=
                            Math.Round(i.Base * i.TasaOCuota!.Value, 6, MotorDeImpuestos.ModoDeRedondeo)))
                        return Inconsistente();
            }
        }

        // Es una copia para calcular el pago; nunca reemplaza la factura emitida.
        var neta = new Comprobante
        {
            Estatus = origen.Estatus, TipoDeComprobante = origen.TipoDeComprobante,
            LugarExpedicion = origen.LugarExpedicion, Moneda = origen.Moneda, Exportacion = origen.Exportacion,
            EmisorRfc = origen.EmisorRfc, EmisorNombre = origen.EmisorNombre, EmisorRegimenFiscal = origen.EmisorRegimenFiscal,
            ReceptorRfc = origen.ReceptorRfc, ReceptorNombre = origen.ReceptorNombre,
            ReceptorRegimenFiscal = origen.ReceptorRegimenFiscal, ReceptorDomicilioFiscal = origen.ReceptorDomicilioFiscal,
            ReceptorUsoCfdi = origen.ReceptorUsoCfdi
        };
        var acreditados = notas.SelectMany(n => n.Conceptos).GroupBy(c => c.Orden).ToDictionary(g => g.Key, g => g.ToArray());
        foreach (var c in origen.Conceptos)
        {
            var creditos = acreditados.GetValueOrDefault(c.Orden) ?? [];
            var restante = c.Importe - c.Descuento - creditos.Sum(n => n.Importe);
            if (restante < 0) return Inconsistente();
            if (restante == 0) continue;
            var concepto = new Concepto
            {
                Orden = c.Orden, ClaveProdServ = c.ClaveProdServ, ClaveUnidad = c.ClaveUnidad,
                Descripcion = c.Descripcion, Cantidad = 1, ValorUnitario = restante,
                Importe = restante, ObjetoImp = c.ObjetoImp
            };
            foreach (var i in c.Impuestos)
            {
                var importe = i.Importe is null ? (decimal?)null : i.Importe.Value -
                    creditos.SelectMany(n => n.Impuestos).Sum(n => n.Importe ?? 0m);
                if (importe < 0) return Inconsistente();
                concepto.Impuestos.Add(new()
                {
                    Impuesto = i.Impuesto, TipoFactor = i.TipoFactor, TasaOCuota = i.TasaOCuota,
                    Base = restante, Importe = importe
                });
            }
            neta.Conceptos.Add(concepto);
        }
        ProyeccionMonetariaCfdi.Calcular(neta, 2, null).AplicarA(neta);
        if (neta.Total != origen.Total - notas.Sum(n => n.Total) || neta.Total < 0) return Inconsistente();
        return neta;
    }

    private static ErrorNegocio Inconsistente() => ErrorNegocio.Conflicto("egresos-desglose-inconsistente",
        "Las notas no coinciden con el desglose disponible de la factura. Revisa sus importes antes de registrar el pago.");
}
