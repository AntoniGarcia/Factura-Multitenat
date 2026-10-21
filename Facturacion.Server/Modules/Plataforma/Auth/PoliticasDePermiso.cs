using Facturacion.Shared.Comun;
using Microsoft.AspNetCore.Authorization;

namespace Facturacion.Server.Modules.Plataforma.Auth;

/// <summary>Registra una política por permiso. Los endpoints usan las claves de Permisos, no roles.</summary>
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

        // Las que se cumplen con cualquiera de varios permisos: el listado lo abre quien emite
        // cualquier documento, y los catálogos los consulta quien los usa para emitir.
        foreach (var (nombre, claves) in Permisos.Politicas.Compuestas)
            constructor.AddPolicy(nombre, politica => politica
                .RequireAuthenticatedUser()
                .RequireAssertion(contexto =>
                    !contexto.User.HasClaim(c => c.Type == ClavesDeClaim.Operador) &&
                    contexto.User.HasClaim(c => c.Type == ClavesDeClaim.Permiso && claves.Contains(c.Value))));

        return constructor;
    }
}
