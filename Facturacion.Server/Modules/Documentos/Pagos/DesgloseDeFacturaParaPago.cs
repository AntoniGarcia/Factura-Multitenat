using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.Pagos;

public sealed record DesgloseDeFacturaParaPago(string ObjetoImpDR, IReadOnlyList<ImpuestoDePagoCalculado> Impuestos)
{
    public static Resultado<string> ObtenerObjeto(Comprobante factura)
    {
        if (factura.Conceptos.Count == 0)
            return ErrorNegocio.Validacion("factura-sin-conceptos", "La factura no tiene conceptos fiscales para calcular su pago.");
        if (ReglasDeObjetoImpuesto.ValidarCombinacion(factura.Conceptos.Select(c => c.ObjetoImp)) is { } combinacion)
            return ErrorNegocio.Validacion("objetos-factura-incompatibles", combinacion);
        foreach (var concepto in factura.Conceptos)
        {
            if (ReglasDeObjetoImpuesto.Validar(concepto.ObjetoImp,
                concepto.Impuestos.Select(i => (i.Impuesto, i.EsRetencion))) is { } errorObjeto)
                return ErrorNegocio.Validacion("impuestos-factura-incompatibles",
                    $"Revisa el concepto {concepto.Orden} de la factura: {errorObjeto}");
            if (concepto.Impuestos.Any(i => i.Base < 0 || i.Impuesto is not ("001" or "002" or "003") ||
                i.TipoFactor is not ("Tasa" or "Cuota" or "Exento") ||
                (i.TipoFactor == "Exento" && (i.EsRetencion || i.TasaOCuota is not null || i.Importe is not null)) ||
                (i.TipoFactor != "Exento" && (i.TasaOCuota is null or < 0 || i.Importe is null or < 0))))
                return ErrorNegocio.Validacion("impuestos-factura-invalidos", "La factura contiene un impuesto incompleto o incompatible con su factor.");
        }
        var objetos = factura.Conceptos.Select(c => c.ObjetoImp).Distinct().ToArray();
        // Una sola clave se conserva; en mezclas la clasificación depende del desglose.
        return objetos.Length == 1 ? objetos[0] : factura.Conceptos.Any(c => c.Impuestos.Count > 0) ? "02" : "01";
    }

    public static Resultado<DesgloseDeFacturaParaPago> Calcular(Comprobante factura, decimal abono)
    {
        var objeto = ObtenerObjeto(factura);
        if (objeto.EsFallo) return objeto.Error!;
        if (factura.Total <= 0 || abono <= 0 || abono > factura.Total)
            return ErrorNegocio.Validacion("importe-pago-invalido", "El abono debe ser positivo y no superar el total de la factura.");
        var grupos = factura.Conceptos.SelectMany(c => c.Impuestos)
            .GroupBy(i => new { i.Impuesto, i.TipoFactor, i.TasaOCuota, i.EsRetencion })
            .Select(g => new ImpuestoDeFactura(g.Key.Impuesto, g.Key.TipoFactor, g.Key.TasaOCuota,
                g.Sum(i => i.Base), g.Any(i => i.Importe is not null) ? g.Sum(i => i.Importe ?? 0m) : null,
                g.Key.EsRetencion)).ToArray();
        IReadOnlyList<ImpuestoDePagoCalculado> impuestos;
        try { impuestos = CalculoDeImpuestosDePago.Repartir(grupos, factura.Total, abono, 6); }
        catch (OverflowException)
        {
            return ErrorNegocio.Validacion("impuestos-factura-fuera-de-rango", "Los impuestos de la factura exceden el rango de cálculo del pago.");
        }
        if (ReglasDeObjetoImpuesto.Validar(objeto.Valor, impuestos.Select(i => (i.Impuesto, i.EsRetencion))) is { } error)
            return ErrorNegocio.Validacion("desglose-pago-no-representable", error);
        return new DesgloseDeFacturaParaPago(objeto.Valor, impuestos);
    }

    public bool CoincideCon(DocumentoPagado documento)
        => ObjetoImpDR == documento.ObjetoImpDR && Ordenar(Impuestos).SequenceEqual(Ordenar(documento.Impuestos.Select(i =>
            new ImpuestoDePagoCalculado(i.Impuesto, i.TipoFactor, i.TasaOCuota, i.Base, i.Importe, i.EsRetencion))));

    private static IOrderedEnumerable<ImpuestoDePagoCalculado> Ordenar(IEnumerable<ImpuestoDePagoCalculado> impuestos)
        => impuestos.OrderBy(i => i.EsRetencion).ThenBy(i => i.Impuesto, StringComparer.Ordinal)
            .ThenBy(i => i.TipoFactor, StringComparer.Ordinal).ThenBy(i => i.TasaOCuota);
}
