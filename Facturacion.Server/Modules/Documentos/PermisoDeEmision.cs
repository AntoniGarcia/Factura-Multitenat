using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;

namespace Facturacion.Server.Modules.Documentos;

// La política del endpoint no conoce el tipo ni la variante; la comprobación exacta es aquí.
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

    // Quien creó el borrador lo edita con Crear; el de otro usuario exige Modificar.
    public static ErrorNegocio? ExigirEdicion(IContextoEmpresa contexto, Comprobante comprobante)
        => comprobante.CreadoPorUsuarioId == contexto.UsuarioId &&
           contexto.Tiene(Para(comprobante.TipoDeComprobante, comprobante.Variante, Permisos.Acciones.Crear))
            ? null
            : Exigir(contexto, comprobante, Permisos.Acciones.Modificar);
}
