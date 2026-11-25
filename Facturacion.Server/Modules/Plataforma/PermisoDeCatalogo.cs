using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma;

// Reglas que la política del endpoint no ve: el estado viene en el formulario y eliminar
// depende de si el registro ya se usó.
public static class PermisoDeCatalogo
{
    // Cambiar «Activo» desde el formulario exige desactivar; si no, Modificar bastaría para dar de baja.
    public static ErrorNegocio? ExigirCambioDeEstado(IContextoEmpresa contexto, string catalogo, bool antes, bool despues)
        => antes == despues || contexto.Tiene(Permisos.De(catalogo, Permisos.Acciones.Desactivar))
            ? null
            : ErrorNegocio.SinPermiso("sin-permiso-desactivar",
                "No tienes permiso para desactivar o reactivar en este catálogo. Pídeselo al titular de la cuenta.");

    // Lo ya usado no se borra: se desactiva.
    public static ErrorNegocio EnUso(string queEs)
        => ErrorNegocio.Conflicto("catalogo-en-uso",
            $"{queEs} ya se usó en algún comprobante y no se puede eliminar. Desactívalo para que ya no se ofrezca.");

    // Segunda barrera: si la comprobación previa no ve un uso, SQL Server rechaza el borrado (547).
    public static bool EsLlaveForanea(DbUpdateException ex)
        => ex.InnerException is SqlException { Number: 547 };
}
