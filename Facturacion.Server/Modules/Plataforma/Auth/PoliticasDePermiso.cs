using Facturacion.Shared.Comun;
using Microsoft.AspNetCore.Authorization;

namespace Facturacion.Server.Modules.Plataforma.Auth;

/// <summary>
/// Los seis permisos de ARQUITECTURA.md §4, como políticas de ASP.NET Core. El nombre de la
/// política es la clave del permiso, así que un endpoint se anota con
/// <c>.RequireAuthorization(Permisos.Timbrar)</c> y no hay cadenas sueltas.
/// <para>
/// No existe ninguna política por rol, y no la habrá: la base de datos ni siquiera tiene
/// tablas de roles.
/// </para>
/// </summary>
public static class PoliticasDePermiso
{
    public static AuthorizationBuilder AgregarPoliticasDePermiso(this AuthorizationBuilder constructor)
    {
        foreach (var permiso in Permisos.Todos)
            constructor.AddPolicy(permiso, politica => politica
                .RequireAuthenticatedUser()
                .RequireClaim(ClavesDeClaim.Permiso, permiso)
                // La barrera en el sentido contrario a la del panel: quien opera el SaaS no
                // actúa dentro de una empresa, así que su token nunca debe abrir un endpoint
                // de inquilino aunque de algún modo llegara a llevar permisos.
                .RequireAssertion(contexto =>
                    !contexto.User.HasClaim(c => c.Type == ClavesDeClaim.Operador)));

        return constructor;
    }
}
