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
                .RequireClaim(ClavesDeClaim.Permiso, permiso));

        return constructor;
    }
}
