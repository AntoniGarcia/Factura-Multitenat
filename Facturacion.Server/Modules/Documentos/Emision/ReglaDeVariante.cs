using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Modules.Documentos.Emision;

/// <summary>
/// Los datos de un complemento solo se guardan en una factura creada para él. Es la defensa del
/// servidor detrás de que el formulario ya no ofrezca activar complementos: sin ella, una factura
/// básica podría terminar timbrada con Notaría u Obra con solo mandar la petición a mano.
/// </summary>
internal static class ReglaDeVariante
{
    public static ErrorNegocio? Exigir(Comprobante comprobante, string variante, string nombre)
        => comprobante.Variante == variante
            ? null
            : ErrorNegocio.Regla("variante-no-corresponde",
                $"Esta factura no se creó como {nombre}. Crea una nueva desde «Nuevo comprobante» con esa opción.");
}
