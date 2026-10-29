using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos;

/// <summary>
/// Qué permiso exige cada acción sobre un comprobante. La política del endpoint solo sabe que se
/// trata de «alguna factura» o «algún documento»; el tipo y la variante viven en el comprobante,
/// así que la comprobación exacta se hace aquí.
/// </summary>
public static class PermisoDeEmision
{
    public static string Para(string tipoDeComprobante, string? variante, string accion)
        => Permisos.De(Permisos.TipoDeDocumento(tipoDeComprobante, variante), accion);

    public static ErrorNegocio? Exigir(IContextoEmpresa contexto, string permiso)
        => contexto.Tiene(permiso)
            ? null
            : ErrorNegocio.SinPermiso(
                "sin-permiso-de-emision",
                $"No tienes permiso para «{Permisos.EtiquetaCorta(permiso)}». Pídeselo al titular de la cuenta.");

    public static ErrorNegocio? Exigir(IContextoEmpresa contexto, Comprobante comprobante, string accion)
        => Exigir(contexto, Para(comprobante.TipoDeComprobante, comprobante.Variante, accion));

    /// <summary>
    /// Editar un borrador: quien lo creó lo edita con Crear, porque el borrador nace vacío y la
    /// captura se guarda después; el de otro usuario exige Modificar. Modificar sirve para los dos.
    /// </summary>
    public static ErrorNegocio? ExigirEdicion(IContextoEmpresa contexto, Comprobante comprobante)
        => comprobante.CreadoPorUsuarioId == contexto.UsuarioId &&
           contexto.Tiene(Para(comprobante.TipoDeComprobante, comprobante.Variante, Permisos.Acciones.Crear))
            ? null
            : Exigir(contexto, comprobante, Permisos.Acciones.Modificar);
}
