using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Impuestos;
using Facturacion.Server.Modules.Documentos.Pagos;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.Egresos;

public sealed record SaldoFiscalParaPago(decimal Total, string ObjetoSinDesglose, IReadOnlyList<ImpuestoDeFactura> Impuestos)
{
    public static Resultado<SaldoFiscalParaPago> Calcular(Comprobante neta, IReadOnlyList<DocumentoPagado> pagos)
    {
        var grupos = neta.Conceptos.SelectMany(c => c.Impuestos)
            .GroupBy(i => (i.Impuesto, i.TipoFactor, i.TasaOCuota, i.EsRetencion))
            .ToDictionary(g => g.Key, g => new ImpuestoDeFactura(g.Key.Impuesto, g.Key.TipoFactor, g.Key.TasaOCuota,
                g.Sum(i => i.Base), g.Key.TipoFactor == "Exento" ? null : g.Sum(i => i.Importe ?? 0m), g.Key.EsRetencion));
        var sinDesglose = neta.Conceptos.Where(c => c.Impuestos.Count == 0).Sum(c => c.Importe - c.Descuento);
        var codigos = neta.Conceptos.Where(c => c.Impuestos.Count == 0).Select(c => c.ObjetoImp).Distinct().ToArray();
        // El margen acumula el redondeo de seis decimales por grupo; no ajusta centavos.
        decimal tolerancia = .000001m;
        foreach (var pago in pagos)
        {
            var margen = .000001m * (pago.Impuestos.Count + 1);
            tolerancia += margen;
            if (pago.MonedaDR != "MXN" || pago.ImpPagado <= 0 || pago.Impuestos.Any(i => i.Base < 0 || i.Importe < 0))
                return Inconsistente();
            var sinImpuestoPagado = pago.ImpPagado - pago.Impuestos.Sum(i => i.Base + (i.Importe ?? 0m));
            if (sinImpuestoPagado < -margen || (codigos.Length == 0 && Math.Abs(sinImpuestoPagado) > margen))
                return Inconsistente();
            sinDesglose -= sinImpuestoPagado;
            foreach (var i in pago.Impuestos)
            {
                var clave = (i.Impuesto, i.TipoFactor, i.TasaOCuota, i.EsRetencion);
                if (!grupos.TryGetValue(clave, out var grupo) || i.EsRetencion ||
                    (i.TipoFactor == "Exento" ? i.Importe is not null : i.Importe is null || i.TasaOCuota is null ||
                        Math.Round(i.Base * i.TasaOCuota.Value, 6, MotorDeImpuestos.ModoDeRedondeo) != i.Importe))
                    return Inconsistente();
                grupos[clave] = grupo with { Base = grupo.Base - i.Base,
                    Importe = grupo.Importe is null ? null : grupo.Importe - i.Importe };
            }
        }
        var total = neta.Total - pagos.Sum(p => p.ImpPagado);
        if (total < 0 || sinDesglose < -tolerancia || grupos.Values.Any(g => g.Base < -tolerancia || g.Importe < -tolerancia))
            return Inconsistente();
        var restantes = new List<ImpuestoDeFactura>();
        foreach (var g in grupos.Values)
        {
            if (g.Base <= tolerancia)
            {
                if (g.Importe > tolerancia) return Inconsistente();
                continue;
            }
            var importe = g.Importe is null ? (decimal?)null : Math.Max(0, g.Importe.Value);
            if (g.TipoFactor == "Tasa" && Math.Abs(importe!.Value -
                Math.Round(g.Base * g.TasaOCuota!.Value, 6, MotorDeImpuestos.ModoDeRedondeo)) > tolerancia)
                return Inconsistente();
            restantes.Add(g with { Importe = importe });
        }
        if (total == 0 && (sinDesglose > tolerancia || restantes.Count > 0)) return Inconsistente();
        return new SaldoFiscalParaPago(total, codigos.Length == 1 ? codigos[0] : "01", restantes);
    }

    public Resultado<DesgloseDeFacturaParaPago> CalcularAbono(decimal abono)
    {
        if (abono <= 0 || abono > Total)
            return ErrorNegocio.Validacion("importe-pago-invalido", "El abono debe ser positivo y no superar el saldo pendiente.");
        var repartidos = CalculoDeImpuestosDePago.Repartir(Impuestos, Total, abono, 6);
        var objeto = Impuestos.Count > 0 ? "02" : ObjetoSinDesglose;
        if (ReglasDeObjetoImpuesto.Validar(objeto, repartidos.Select(i => (i.Impuesto, i.EsRetencion))) is { } error)
            return ErrorNegocio.Validacion("desglose-pago-no-representable", error);
        return new DesgloseDeFacturaParaPago(objeto, repartidos);
    }

    private static ErrorNegocio Inconsistente() => ErrorNegocio.Conflicto("saldo-fiscal-no-disponible",
        "Las bases o impuestos pendientes no alcanzan para esta operación. Revisa las notas, pagos y sus redondeos.");
}
