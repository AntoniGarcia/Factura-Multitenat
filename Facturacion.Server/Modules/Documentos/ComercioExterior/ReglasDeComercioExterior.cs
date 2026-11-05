using System.Text.RegularExpressions;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.ComercioExterior;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.ComercioExterior;

public static class ReglasDeComercioExterior
{
    private const string PatronCurp = "\\A[A-Z][AEIOUX][A-Z]{2}[0-9]{2}(0[1-9]|1[012])(0[1-9]|[12][0-9]|3[01])[MHX]([ABCMTZ]S|[BCJMOT]C|[CNPST]L|[GNQ]T|[GQS]R|C[MH]|[MY]N|[DH]G|NE|VZ|DF|SP)[BCDFGHJ-NP-TV-Z]{3}[0-9A-Z][0-9]\\z";
    public static ErrorNegocio? ValidarCabecera(Comprobante comprobante, DatosComercioExteriorDto datos, bool completo)
    {
        if (comprobante.GlobalPeriodicidad is not null || comprobante.GlobalMeses is not null || comprobante.GlobalAnio is not null)
            return ErrorNegocio.Validacion("comercio-informacion-global", "Comercio Exterior no admite información global.");
        if (!datos.CertificadoOrigen && !string.IsNullOrWhiteSpace(datos.NumeroCertificadoOrigen))
            return ErrorNegocio.Validacion("comercio-certificado-no-aplica", "Quita el número de certificado o activa certificado de origen.");
        if (completo && string.IsNullOrWhiteSpace(datos.Incoterm))
            return ErrorNegocio.Validacion("comercio-incoterm-obligatorio", "Selecciona el INCOTERM de la exportación definitiva.");
        if (comprobante.EmisorRfc.Length == 12 && !string.IsNullOrWhiteSpace(datos.CurpEmisor))
            return ErrorNegocio.Validacion("comercio-curp-no-aplica", "La CURP del emisor solo corresponde a personas físicas.");
        if (comprobante.EmisorRfc.Length == 13 && (completo || !string.IsNullOrWhiteSpace(datos.CurpEmisor)) &&
            !Regex.IsMatch(datos.CurpEmisor ?? "", PatronCurp, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            return ErrorNegocio.Validacion("comercio-curp-obligatoria", "Captura una CURP válida del emisor persona física.");
        if (comprobante.Moneda == "USD" && comprobante.TipoCambio is { } cambio && datos.TipoCambioUsd is { } usd && cambio != usd)
            return ErrorNegocio.Validacion("comercio-tipo-cambio-usd", "El tipo de cambio de la factura en USD debe coincidir con el del complemento.");
        return null;
    }

    public static ErrorNegocio? ValidarMercancia(Concepto concepto, MercanciaComercioExteriorDto mercancia)
    {
        var servicio = mercancia.UnidadAduana == "99" || concepto.ClaveUnidad == "E48";
        if (servicio && !string.IsNullOrWhiteSpace(mercancia.FraccionArancelaria))
            return ErrorNegocio.Validacion("comercio-fraccion-no-aplica", "Los servicios E48 o con unidad aduanera 99 no llevan fracción arancelaria.");
        if (!servicio && string.IsNullOrWhiteSpace(mercancia.FraccionArancelaria))
            return ErrorNegocio.Validacion("comercio-fraccion-obligatoria", "Selecciona la fracción arancelaria de cada mercancía.");
        if (mercancia.UnidadAduana is not (null or "99") && mercancia.ValorUnitarioAduana is <= 0)
            return ErrorNegocio.Validacion("comercio-valor-unitario-aduana", "El valor unitario aduanero debe ser mayor que cero para bienes.");
        if (mercancia.CantidadAduana is { } cantidad && mercancia.ValorUnitarioAduana is { } unitario &&
            mercancia.ValorDolares is { } valor && valor != 1m)
        {
            try
            {
                // Cantidad y precio se serializan con tres y seis decimales, respectivamente.
                var minimo = Truncar(Math.Max(0, cantidad - 0.0005m) * Math.Max(0, unitario - 0.0000005m));
                var maximo = Subir((cantidad + 0.0005m - 0.000000000001m) * (unitario + 0.0000005m - 0.000000000001m));
                if (valor < minimo || valor > maximo)
                    return ErrorNegocio.Validacion("comercio-valor-aduana-inconsistente", "El valor en dólares está fuera del rango de cantidad por precio aduaneros.");
            }
            catch (OverflowException)
            {
                return ErrorNegocio.Validacion("comercio-importe-excesivo", "Reduce las cantidades o valores aduaneros.");
            }
        }
        return null;
    }

    public static ErrorNegocio? ValidarConversion(Comprobante comprobante, DatosComercioExteriorDto datos, int decimalesMoneda)
    {
        if (datos.TipoCambioUsd is not > 0 || datos.Mercancias is null) return null;
        var cambio = comprobante.Moneda switch { "USD" => datos.TipoCambioUsd, "MXN" => 1m, _ => comprobante.TipoCambio };
        if (cambio is not > 0) return ErrorNegocio.Validacion("comercio-conversion-sin-cambio", "Falta el tipo de cambio de la factura para convertir a USD.");
        foreach (var grupo in datos.Mercancias.GroupBy(m => comprobante.Conceptos.First(c => c.Orden == m.OrdenConcepto).NoIdentificacion))
        {
            if (grupo.Any(m => m.ValorDolares is null or 0 or 1)) continue;
            try
            {
                var conceptos = comprobante.Conceptos.Where(c => c.NoIdentificacion == grupo.Key).ToArray();
                var importe = conceptos.Sum(c => decimal.Round(c.Importe, decimalesMoneda, MidpointRounding.ToEven));
                if (grupo.Any(m => m.FraccionArancelaria == "9801000100"))
                    importe -= conceptos.Sum(c => decimal.Round(c.Descuento, decimalesMoneda, MidpointRounding.ToEven));
                var medioImporte = conceptos.Length * Paso(decimalesMoneda) / 2m;
                var medioCambio = comprobante.Moneda == "MXN" ? 0m : Paso(6) / 2m;
                var medioUsd = Paso(6) / 2m;
                var inferior = Truncar(Math.Max(0, importe - medioImporte) * Math.Max(0, cambio.Value - medioCambio) /
                    (datos.TipoCambioUsd.Value + medioUsd - 0.000000000001m));
                var superior = Subir((importe + medioImporte - 0.000000000001m) * (cambio.Value + medioCambio - 0.000000000001m) /
                    (datos.TipoCambioUsd.Value - medioUsd));
                var suma = grupo.Sum(m => m.ValorDolares!.Value);
                if (suma < inferior || suma > superior)
                    return ErrorNegocio.Validacion("comercio-conversion-inconsistente", "Los valores en USD no corresponden al importe de los conceptos y sus tipos de cambio.");
            }
            catch (OverflowException)
            {
                return ErrorNegocio.Validacion("comercio-importe-excesivo", "Reduce los importes de Comercio Exterior.");
            }
        }
        return null;
    }

    private static decimal Paso(int decimales)
    {
        var paso = 1m;
        for (var i = 0; i < decimales; i++) paso /= 10m;
        return paso;
    }
    private static decimal Truncar(decimal valor) => decimal.Truncate(valor * 100m) / 100m;
    private static decimal Subir(decimal valor) => decimal.Ceiling(valor * 100m) / 100m;
}
