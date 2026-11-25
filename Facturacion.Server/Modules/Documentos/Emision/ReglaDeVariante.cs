using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.Emision;

// Los datos de un complemento solo se guardan en una factura creada para él, aunque la
// petición llegue armada a mano.
internal static class ReglaDeVariante
{
    public static ErrorNegocio? Exigir(Comprobante comprobante, string variante, string nombre)
        => comprobante.Variante == variante
            ? null
            : ErrorNegocio.Regla("variante-no-corresponde",
                $"Esta factura no se creó como {nombre}. Crea una nueva desde «Nuevo comprobante» con esa opción.");
}
