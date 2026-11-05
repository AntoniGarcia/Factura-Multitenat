namespace Facturacion.Shared.Documentos;

public static class ConversionDePago
{
    public static bool EsEquivalenciaValida(string monedaDocumento, string monedaPago, decimal? equivalencia)
        => monedaDocumento == monedaPago ? equivalencia is null or 1m :
            equivalencia is >= 0.0000000001m and <= 999999999999.9999999999m && Math.Round(equivalencia.Value, 10) == equivalencia;

    public static decimal AMonedaDelPago(decimal abono, decimal equivalencia) => abono / equivalencia;

    public static decimal Redondear(decimal importe, int decimales)
        => Math.Round(importe, decimales, MidpointRounding.ToEven);

    public static bool Cuadra(decimal monto, int decimalesPago,
        IEnumerable<(decimal Abono, decimal Equivalencia, int Decimales, bool MismaMoneda)> renglones)
    {
        var filas = renglones.ToArray();
        if (decimalesPago is < 0 or > 6 || filas.Any(x => x.Abono is <= 0 or > 999999999999.999999m ||
            x.Equivalencia is < 0.0000000001m or > 999999999999.9999999999m || x.Decimales is < 0 or > 6)) return false;
        var total = filas.Sum(x => AMonedaDelPago(x.Abono, x.Equivalencia));
        if (total > 999999999999.999999m) return false;
        if (filas.All(x => x.MismaMoneda)) return monto == total;
        var escala = Escala(decimalesPago);
        var truncado = decimal.Truncate(total * escala) / escala;
        // El reparto es completo: solo admite la fracción de unidad causada por convertir.
        if (monto >= truncado && monto <= decimal.Ceiling(total * escala) / escala) return true;
        if (monto >= truncado) return false;
        const decimal mediaEquivalencia = 0.00000000005m;
        const decimal epsilon = 0.000000000000000001m;
        var inferior = filas.Sum(x => decimal.Truncate((x.Abono - 1m / Escala(x.Decimales) / 2m) /
            (x.Equivalencia + mediaEquivalencia - epsilon) * escala) / escala);
        var superior = filas.Sum(x => Redondear((x.Abono + 1m / Escala(x.Decimales) / 2m - epsilon) /
            (x.Equivalencia - mediaEquivalencia), decimalesPago));
        return monto >= inferior && monto <= superior;
    }

    private static decimal Escala(int decimales)
    {
        var valor = 1m;
        for (var i = 0; i < decimales; i++) valor *= 10;
        return valor;
    }
}
