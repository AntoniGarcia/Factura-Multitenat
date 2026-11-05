using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Impuestos;
using Facturacion.Server.Modules.Documentos.Pagos;
using Facturacion.Server.Modules.Documentos.Salidas;
using System.Xml;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Documentos;

namespace Facturacion.Server.Modules.Documentos.Egresos;

public static class CalculoDeNotaCredito
{
    public const string Variante = "nota_credito";

    public static ErrorNegocio? ValidarOrigen(Comprobante origen)
    {
        if (origen.TipoDeComprobante != "I" || origen.Estatus != "timbrado" || origen.Uuid is null)
            return ErrorNegocio.Validacion("origen-nota-no-vigente", "Selecciona una factura de ingreso timbrada y vigente.");
        if (origen.MetodoPago is not ("PUE" or "PPD") || origen.Moneda != "MXN" || origen.Exportacion != "01" ||
            origen.Variante is not (null or "basica") || origen.TasaIsh is not null || origen.GlobalPeriodicidad is not null ||
            origen.ReceptorRfc is "XAXX010101000" or "XEXX010101000")
            return ErrorNegocio.Validacion("origen-nota-no-admitido", "Esta fase admite facturas básicas PUE o PPD en MXN, nominativas y sin impuestos locales.");
        var objeto = DesgloseDeFacturaParaPago.ObtenerObjeto(origen);
        if (objeto.EsFallo) return objeto.Error!;
        if (origen.Total <= 0 || origen.Conceptos.Select(c => c.Orden).Distinct().Count() != origen.Conceptos.Count ||
            origen.Conceptos.Any(c => c.Importe < c.Descuento || c.Impuestos.Count > 1 || c.Impuestos.Any(i => i.EsRetencion ||
                i.Impuesto != "002" || i.TipoFactor is not ("Tasa" or "Exento") ||
                i.Base != c.Importe - c.Descuento)))
            return ErrorNegocio.Validacion("impuestos-nota-no-admitidos", "Esta fase admite IVA y conceptos sin desglose, sin retenciones ni IEPS.");
        if (!ProyeccionMonetariaCfdi.Calcular(origen, 2, null).CoincideCon(origen) ||
            origen.Conceptos.SelectMany(c => c.Impuestos).Any(i => i.TipoFactor == "Tasa" &&
                Math.Round(i.Base * i.TasaOCuota!.Value, 6, MotorDeImpuestos.ModoDeRedondeo) != i.Importe))
            return ErrorNegocio.Validacion("origen-nota-importes-inconsistentes", "Los importes de la factura original requieren revisión antes de acreditarlos.");
        if (origen.Conceptos.Any(c => Redondear(c.Importe) - Redondear(c.Descuento) != c.Importe - c.Descuento ||
            c.Impuestos.Any(i => i.TipoFactor == "Tasa" && Redondear(i.Importe!.Value) != Redondear((c.Importe - c.Descuento) * i.TasaOCuota!.Value))))
            return ErrorNegocio.Validacion("origen-nota-redondeo", "Esta modalidad requiere bases netas en centavos y un desglose de IVA sin diferencias de redondeo.");
        return null;
    }

    public static bool TieneTratamientosMixtos(Comprobante origen)
        => origen.Conceptos.Where(c => c.Importe > c.Descuento)
            .Select(c => (c.ObjetoImp, Factor: c.Impuestos.FirstOrDefault()?.TipoFactor,
                Tasa: c.Impuestos.FirstOrDefault()?.TasaOCuota)).Distinct().Count() > 1;

    public static Resultado<IReadOnlyList<Concepto>> Calcular(Comprobante origen, PeticionGuardarNotaCredito peticion,
        IReadOnlyDictionary<int, decimal> acreditado, IReadOnlyDictionary<int, decimal>? ivaAcreditado = null)
    {
        if (ValidarOrigen(origen) is { } errorOrigen) return errorOrigen;
        if (peticion.TipoRelacion is not ("01" or "03"))
            return ErrorNegocio.Validacion("relacion-nota-invalida", "Selecciona bonificación o devolución: relación 01 o 03.");
        if (peticion.Conceptos is null || peticion.Conceptos.Count is 0 or > 1000 ||
            peticion.Conceptos.Select(c => c.OrdenOriginal).Distinct().Count() != peticion.Conceptos.Count)
            return ErrorNegocio.Validacion("conceptos-nota-invalidos", "Agrega conceptos sin repetirlos, hasta un máximo de 1000.");
        var resultado = new List<Concepto>(peticion.Conceptos.Count);
        foreach (var linea in peticion.Conceptos)
        {
            var original = origen.Conceptos.SingleOrDefault(c => c.Orden == linea.OrdenOriginal);
            if (original is null || linea.BaseAcreditar <= 0 || Redondear(linea.BaseAcreditar) != linea.BaseAcreditar)
                return ErrorNegocio.Validacion("importe-nota-invalido", "Selecciona conceptos de la factura e importes positivos con hasta dos decimales.");
            var baseOriginal = original.Importe - original.Descuento;
            if (linea.BaseAcreditar > baseOriginal - acreditado.GetValueOrDefault(original.Orden))
                return ErrorNegocio.Conflicto("nota-supera-disponible", "El importe supera lo disponible del concepto. Actualiza la factura y revisa sus notas anteriores.");
            var usado = acreditado.GetValueOrDefault(original.Orden);
            // Evita que varias devoluciones parciales acumulen un centavo extra o pendiente de IVA.
            if (original.Impuestos.Any(i => i.TipoFactor == "Tasa" && (
                Redondear(Math.Round(linea.BaseAcreditar * i.TasaOCuota!.Value, 6, MotorDeImpuestos.ModoDeRedondeo)) !=
                Redondear((usado + linea.BaseAcreditar) * i.TasaOCuota!.Value) -
                (ivaAcreditado?.GetValueOrDefault(original.Orden) ?? Redondear(usado * i.TasaOCuota!.Value)) ||
                Redondear((usado + linea.BaseAcreditar) * i.TasaOCuota!.Value) +
                Redondear((baseOriginal - usado - linea.BaseAcreditar) * i.TasaOCuota!.Value) != Redondear(i.Importe!.Value))))
                return ErrorNegocio.Validacion("nota-redondeo-parcial", "Este importe produce una diferencia de IVA con las notas anteriores. Ajusta la base en centavos o revisa esas notas.");
            var descripcion = linea.Descripcion?.Trim() ?? original.Descripcion;
            if (descripcion.Length is 0 or > 1000 || descripcion.Contains('|'))
                return ErrorNegocio.Validacion("descripcion-nota-invalida", "La descripción admite hasta 1000 caracteres y no permite plecas.");
            try { XmlConvert.VerifyXmlChars(descripcion); }
            catch (XmlException) { return ErrorNegocio.Validacion("descripcion-nota-invalida", "La descripción contiene caracteres no admitidos."); }
            resultado.Add(new Concepto
            {
                Id = Guid.NewGuid(), Orden = original.Orden, ClaveProdServ = "84111506", ClaveUnidad = "ACT",
                Descripcion = descripcion, Cantidad = 1, ValorUnitario = linea.BaseAcreditar, Importe = linea.BaseAcreditar,
                ObjetoImp = original.ObjetoImp,
                DatosFiscalesJson = original.DatosFiscalesJson,
                Impuestos = original.Impuestos.Select(i => new ImpuestoConcepto
                {
                    Id = Guid.NewGuid(), Impuesto = i.Impuesto, TipoFactor = i.TipoFactor,
                    TasaOCuota = i.TasaOCuota, Base = linea.BaseAcreditar,
                    Importe = i.TipoFactor == "Exento" ? null : Math.Round(linea.BaseAcreditar * i.TasaOCuota!.Value, 6, MotorDeImpuestos.ModoDeRedondeo)
                }).ToList()
            });
        }
        return resultado;
    }

    private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MotorDeImpuestos.ModoDeRedondeo);
}
