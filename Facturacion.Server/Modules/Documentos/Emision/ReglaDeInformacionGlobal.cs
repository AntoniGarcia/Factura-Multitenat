using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.Emision;

/// <summary>
/// CFDI40130: una factura de ingreso a XAXX010101000 con el nombre PUBLICO EN GENERAL es una
/// factura global y lleva InformacionGlobal. Con otro nombre es una venta suelta y no la lleva.
/// Se revisa al guardar y otra vez antes de apartar folio, por los borradores guardados antes.
/// </summary>
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
