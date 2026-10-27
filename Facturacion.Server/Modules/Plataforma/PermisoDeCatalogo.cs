using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma;

/// <summary>
/// Reglas comunes a los catálogos que la política del endpoint no alcanza a ver: el formulario
/// de alta y edición también trae el estado, y eliminar depende de si el registro ya se usó.
/// </summary>
public static class PermisoDeCatalogo
{
    /// <summary>
    /// Cambiar «Activo» desde el formulario es desactivar o reactivar, aunque llegue por el
    /// endpoint de crear o de modificar: sin esta comprobación Modificar bastaría para dar de baja.
    /// </summary>
    public static ErrorNegocio? ExigirCambioDeEstado(IContextoEmpresa contexto, string catalogo, bool antes, bool despues)
        => antes == despues || contexto.Tiene(Permisos.De(catalogo, Permisos.Acciones.Desactivar))
            ? null
            : ErrorNegocio.SinPermiso("sin-permiso-desactivar",
                "No tienes permiso para desactivar o reactivar en este catálogo. Pídeselo al titular de la cuenta.");

    /// <summary>
    /// Respuesta cuando el registro ya se usó. Nada usado se borra (AGENTS.md §5): se desactiva.
    /// </summary>
    public static ErrorNegocio EnUso(string queEs)
        => ErrorNegocio.Conflicto("catalogo-en-uso",
            $"{queEs} ya se usó en algún comprobante y no se puede eliminar. Desactívalo para que ya no se ofrezca.");

    /// <summary>
    /// La llave foránea es la segunda barrera: si un uso nuevo no está en la comprobación previa,
    /// SQL Server rechaza el borrado (error 547) y se responde igual que si se hubiera detectado.
    /// </summary>
    public static bool EsLlaveForanea(DbUpdateException ex)
        => ex.InnerException is SqlException { Number: 547 };
}
