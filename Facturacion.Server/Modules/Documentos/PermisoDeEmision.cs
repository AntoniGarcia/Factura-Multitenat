using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos;

/// <summary>
/// Qué permiso exige crear, editar, descartar o timbrar un comprobante. La política del endpoint
/// solo sabe que se trata de «alguna factura» o «algún documento»; el tipo y la variante viven en
/// el comprobante, así que la comprobación exacta se hace aquí.
/// </summary>
public static class PermisoDeEmision
{
    public static string Para(string tipoDeComprobante, string? variante) => tipoDeComprobante switch
    {
        "P" => Permisos.EmitirPago,
        "T" => Permisos.EmitirCartaPorte,
        _ => Permisos.ParaVarianteDeFactura(variante)
    };

    public static string Para(Comprobante comprobante) => Para(comprobante.TipoDeComprobante, comprobante.Variante);

    public static ErrorNegocio? Exigir(IContextoEmpresa contexto, string permiso)
        => contexto.Tiene(permiso)
            ? null
            : ErrorNegocio.SinPermiso(
                "sin-permiso-de-emision",
                $"No tienes permiso para emitir «{Permisos.EtiquetaCorta(permiso)}». " +
                "Pídeselo al titular de la cuenta.");

    public static ErrorNegocio? Exigir(IContextoEmpresa contexto, Comprobante comprobante)
        => Exigir(contexto, Para(comprobante));
}
