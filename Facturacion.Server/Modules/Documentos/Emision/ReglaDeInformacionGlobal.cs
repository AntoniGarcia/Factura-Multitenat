using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.Emision;

/// CFDI40130: a XAXX010101000 con el nombre PUBLICO EN GENERAL es factura global y lleva
/// InformacionGlobal; con otro nombre es venta suelta. El borrador se guarda sin ella (el
/// formulario avisa) y se exige antes de apartar folio.
internal static class ReglaDeInformacionGlobal
{
    public const string PublicoEnGeneral = "PUBLICO EN GENERAL";

    public static ErrorNegocio? Validar(string? rfcReceptor, string? nombreReceptor, string? periodicidad)
        => rfcReceptor == Rfc.GenericoNacional && nombreReceptor == PublicoEnGeneral && periodicidad is null
            ? ErrorNegocio.Validacion("global-requerida",
                "A nombre de PUBLICO EN GENERAL el SAT pide los datos de la factura global. Captúralos en " +
                "«Información global» o, si es una venta suelta, escribe el nombre del comprador.")
            : null;
}
