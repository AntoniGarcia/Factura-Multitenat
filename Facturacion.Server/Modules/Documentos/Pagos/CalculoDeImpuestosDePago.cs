using Facturacion.Server.Modules.Documentos.Impuestos;

namespace Facturacion.Server.Modules.Documentos.Pagos;

/// <summary>Un grupo de impuesto de la factura que se está abonando, ya agregado.</summary>
/// <param name="Base">Base gravable del grupo en la factura completa.</param>
/// <param name="Importe">Importe del grupo en la factura completa. Nulo si es exento.</param>
public sealed record ImpuestoDeFactura(
    string Impuesto,
    string TipoFactor,
    decimal? TasaOCuota,
    decimal Base,
    decimal? Importe,
    bool EsRetencion);

/// <summary>Un renglón de <c>ImpuestosDR</c> ya calculado.</summary>
public sealed record ImpuestoDePagoCalculado(
    string Impuesto,
    string TipoFactor,
    decimal? TasaOCuota,
    decimal Base,
    decimal? Importe,
    bool EsRetencion);

/// <summary>
/// Reparte los impuestos de una factura entre lo que se abona en un pago
/// (<c>ImpuestosDR</c> del complemento de pagos 2.0).
///
/// <para><b>Por qué no sirve el motor de B1</b></para>
/// <see cref="MotorDeImpuestos"/> calcula hacia adelante: cantidad × valor unitario, y de ahí
/// la base y el impuesto. Aquí el problema es el inverso y es otro: de estos $5,000 abonados
/// contra una factura de $11,600, ¿qué parte era base y qué parte era IVA? No hay conceptos
/// que recorrer, solo una proporción.
///
/// <para><b>Se reparte proporcional, no se recalcula desde cero</b></para>
/// La proporción es <c>importe pagado ÷ total de la factura</c>, y se aplica a la base de cada
/// grupo de impuesto. Así una factura con renglones al 16 %, al 0 % y exentos reparte cada
/// grupo en su justa parte, en vez de suponer que todo iba a la misma tasa —que es lo que
/// falla en cuanto una factura mezcla tasas—.
///
/// <para>
/// El importe se <b>recalcula</b> como base × tasa en vez de escalarse, porque esa es
/// justamente la comprobación que hace el SAT sobre cada renglón. Escalar el importe original
/// daría diferencias de un centavo contra su propia cuenta, y eso es rechazo.
/// </para>
///
/// <para><b>Pagar la factura completa devuelve exactamente sus números</b></para>
/// Con proporción 1 el reparto es la identidad. Es la propiedad que hace comprobable el
/// cálculo sin depender del SAT: el caso más común tiene que reproducir la factura tal cual.
/// </para>
/// </summary>
public static class CalculoDeImpuestosDePago
{
    /// <param name="impuestosDeLaFactura">Grupos de impuesto de la factura completa.</param>
    /// <param name="totalDeLaFactura">Total de la factura, contra el que se saca la proporción.</param>
    /// <param name="importePagado">Lo que se abona en este pago.</param>
    /// <param name="decimalesDeMoneda">Decimales de <c>c_Moneda</c> del documento pagado.</param>
    public static IReadOnlyList<ImpuestoDePagoCalculado> Repartir(
        IReadOnlyList<ImpuestoDeFactura> impuestosDeLaFactura,
        decimal totalDeLaFactura,
        decimal importePagado,
        int decimalesDeMoneda)
    {
        if (impuestosDeLaFactura.Count == 0 || totalDeLaFactura <= 0 || importePagado <= 0)
            return [];

        // Se acota a 1: abonar de más no puede declarar más impuesto del que la factura llevaba.
        // Que el importe no exceda el saldo lo valida quien construye el pago; aquí solo se
        // impide que un dato malo se convierta en un XML absurdo.
        var proporcion = Math.Min(importePagado / totalDeLaFactura, 1m);

        var repartidos = new List<ImpuestoDePagoCalculado>(impuestosDeLaFactura.Count);

        foreach (var grupo in impuestosDeLaFactura)
        {
            var baseDr = Math.Round(
                grupo.Base * proporcion, decimalesDeMoneda, MotorDeImpuestos.ModoDeRedondeo);

            if (baseDr <= 0) continue;

            repartidos.Add(new ImpuestoDePagoCalculado(
                grupo.Impuesto,
                grupo.TipoFactor,
                grupo.TasaOCuota,
                baseDr,
                ImporteDe(grupo, baseDr, proporcion, decimalesDeMoneda),
                grupo.EsRetencion));
        }

        return repartidos;
    }

    /// <summary>
    /// Un exento no lleva importe —declararlo en cero es rechazo—, y una cuota es un monto
    /// fijo por unidad, no un porcentaje: multiplicarla por la base daría un número sin
    /// sentido, así que esa sí se escala.
    /// </summary>
    private static decimal? ImporteDe(
        ImpuestoDeFactura grupo, decimal baseDr, decimal proporcion, int decimales)
        => grupo.TipoFactor switch
        {
            TiposDeFactor.Exento => null,
            TiposDeFactor.Cuota => grupo.Importe is { } importe
                ? Math.Round(importe * proporcion, decimales, MotorDeImpuestos.ModoDeRedondeo)
                : null,
            _ => grupo.TasaOCuota is { } tasa
                ? Math.Round(baseDr * tasa, decimales, MotorDeImpuestos.ModoDeRedondeo)
                : null
        };

    /// <summary>
    /// Los <c>Totales</c> del complemento: el SAT los pide desglosados por tasa, con un
    /// atributo distinto para cada una de las tasas usuales de IVA.
    /// </summary>
    /// <param name="montoTotalPagos">Suma de los montos de todos los pagos del comprobante.</param>
    public static TotalesDePago Totalizar(
        IReadOnlyList<ImpuestoDePagoCalculado> impuestos, decimal montoTotalPagos, int decimalesDeMoneda)
    {
        decimal Sumar(Func<ImpuestoDePagoCalculado, bool> filtro, Func<ImpuestoDePagoCalculado, decimal?> campo)
            => Math.Round(
                impuestos.Where(filtro).Sum(i => campo(i) ?? 0m),
                decimalesDeMoneda,
                MotorDeImpuestos.ModoDeRedondeo);

        bool EsIvaTrasladadoA(ImpuestoDePagoCalculado i, decimal tasa)
            => !i.EsRetencion && i.Impuesto == ClavesDeImpuesto.Iva &&
               i.TipoFactor == TiposDeFactor.Tasa && i.TasaOCuota == tasa;

        bool EsIvaExento(ImpuestoDePagoCalculado i)
            => !i.EsRetencion && i.Impuesto == ClavesDeImpuesto.Iva && i.TipoFactor == TiposDeFactor.Exento;

        return new TotalesDePago(
            RetencionesIva: Sumar(i => i.EsRetencion && i.Impuesto == ClavesDeImpuesto.Iva, i => i.Importe),
            RetencionesIsr: Sumar(i => i.EsRetencion && i.Impuesto == ClavesDeImpuesto.Isr, i => i.Importe),
            RetencionesIeps: Sumar(i => i.EsRetencion && i.Impuesto == ClavesDeImpuesto.Ieps, i => i.Importe),
            BaseIva16: Sumar(i => EsIvaTrasladadoA(i, 0.16m), i => i.Base),
            ImpuestoIva16: Sumar(i => EsIvaTrasladadoA(i, 0.16m), i => i.Importe),
            BaseIva8: Sumar(i => EsIvaTrasladadoA(i, 0.08m), i => i.Base),
            ImpuestoIva8: Sumar(i => EsIvaTrasladadoA(i, 0.08m), i => i.Importe),
            BaseIva0: Sumar(i => EsIvaTrasladadoA(i, 0m), i => i.Base),
            ImpuestoIva0: Sumar(i => EsIvaTrasladadoA(i, 0m), i => i.Importe),
            BaseIvaExento: Sumar(EsIvaExento, i => i.Base),
            MontoTotalPagos: Math.Round(montoTotalPagos, decimalesDeMoneda, MotorDeImpuestos.ModoDeRedondeo));
    }
}

/// <summary>
/// El nodo <c>Totales</c> del complemento. Cada campo es un atributo opcional del XML: se
/// omite cuando va en cero, porque declarar una base de IVA al 8 % que no existe es rechazo.
/// </summary>
public sealed record TotalesDePago(
    decimal RetencionesIva,
    decimal RetencionesIsr,
    decimal RetencionesIeps,
    decimal BaseIva16,
    decimal ImpuestoIva16,
    decimal BaseIva8,
    decimal ImpuestoIva8,
    decimal BaseIva0,
    decimal ImpuestoIva0,
    decimal BaseIvaExento,
    decimal MontoTotalPagos);

/// <summary>Las tres claves de <c>c_Impuesto</c>.</summary>
public static class ClavesDeImpuesto
{
    public const string Isr = "001";
    public const string Iva = "002";
    public const string Ieps = "003";
}
