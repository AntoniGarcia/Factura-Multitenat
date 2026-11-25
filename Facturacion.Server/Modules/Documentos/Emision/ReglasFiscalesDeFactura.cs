using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos.Emision;

/// Reglas de la matriz de errores CFDI 4.0 del SAT (revisión del 25 de marzo de 2026) que el XSD
/// no revisa y que el PAC rechazaría ya con folio apartado. Se aplican al guardar la factura y
/// otra vez antes de apartar folio, por los borradores guardados antes de que existieran.
internal static class ReglasFiscalesDeFactura
{
    private static readonly string[] MesesNormales = ["01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12"];
    private static readonly string[] MesesBimestrales = ["13", "14", "15", "16", "17", "18"];

    public static async Task<ErrorNegocio?> ValidarAsync(
        Comprobante factura, IServicioCatalogosSat catalogos, int anioLocal, CancellationToken ct)
    {
        // CFDI40105
        if (factura.MetodoPago == "PPD" && factura.FormaPago != "99")
            return ErrorNegocio.Validacion("ppd-requiere-forma-99",
                "Con método de pago PPD (en parcialidades o diferido) la forma de pago es 99, Por definir.");

        // CFDI40159 y CFDI40152. El catálogo de clientes ya lo exige; esto cubre clientes dados de alta antes.
        if (factura.ReceptorRfc is Rfc.GenericoNacional or Rfc.GenericoExtranjero && factura.ReceptorRegimenFiscal != "616")
            return ErrorNegocio.Validacion("generico-regimen",
                $"Con el RFC {factura.ReceptorRfc} el régimen del receptor es 616, Sin obligaciones fiscales. Corrígelo en el cliente y vuelve a guardar.");
        if (factura.ReceptorRfc == Rfc.GenericoExtranjero && factura.ReceptorResidenciaFiscal == "MEX")
            return ErrorNegocio.Validacion("extranjero-residencia-mex",
                "La residencia fiscal de un cliente extranjero no puede ser México. Corrígela en el cliente y vuelve a guardar.");

        // CFDI40161. Sin uso o sin régimen todavía es un borrador incompleto: el XSD lo detiene al timbrar.
        if (!string.IsNullOrEmpty(factura.ReceptorUsoCfdi) && !string.IsNullOrEmpty(factura.ReceptorRegimenFiscal) &&
            !await catalogos.EsUsoCfdiCompatibleAsync(factura.ReceptorUsoCfdi, factura.ReceptorRegimenFiscal,
                Rfc.EsPersonaMoral(factura.ReceptorRfc), ct))
            return ErrorNegocio.Validacion("uso-cfdi-incompatible",
                $"El uso de CFDI {factura.ReceptorUsoCfdi} no corresponde al régimen {factura.ReceptorRegimenFiscal} " +
                $"de una persona {(Rfc.EsPersonaMoral(factura.ReceptorRfc) ? "moral" : "física")}. Elige otro uso.");

        return factura.GlobalPeriodicidad is { } periodicidad
            ? ValidarInformacionGlobal(factura, periodicidad, anioLocal)
            : null;
    }

    private static ErrorNegocio? ValidarInformacionGlobal(Comprobante factura, string periodicidad, int anioLocal)
    {
        var bimestral = periodicidad == "05";

        // CFDI40132
        if (bimestral && factura.EmisorRegimenFiscal != "621")
            return ErrorNegocio.Validacion("global-bimestral-regimen",
                "La periodicidad bimestral solo la usa el régimen 621, Incorporación Fiscal. Elige otra periodicidad.");

        // CFDI40134 y CFDI40135
        if (bimestral ? !MesesBimestrales.Contains(factura.GlobalMeses) : !MesesNormales.Contains(factura.GlobalMeses))
            return ErrorNegocio.Validacion("global-meses",
                bimestral
                    ? "Con periodicidad bimestral el periodo va de 13 (enero-febrero) a 18 (noviembre-diciembre)."
                    : "Elige el mes de la factura global, de 01 (enero) a 12 (diciembre).");

        // CFDI40136
        if (factura.GlobalAnio is not { } anio || anio > anioLocal || anio < anioLocal - 5)
            return ErrorNegocio.Validacion("global-anio",
                $"El año de la factura global va de {anioLocal - 5} a {anioLocal}.");

        return null;
    }
}
