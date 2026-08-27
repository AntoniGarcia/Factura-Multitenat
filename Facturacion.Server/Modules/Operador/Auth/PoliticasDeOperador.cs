using Facturacion.Shared.Comun;
using Microsoft.AspNetCore.Authorization;

namespace Facturacion.Server.Modules.Operador.Auth;

/// <summary>Nombres de los esquemas de autenticación del sistema.</summary>
public static class EsquemasDeAutenticacion
{
    /// <summary>
    /// El del panel de operador. Es un esquema aparte del predeterminado porque valida una
    /// audiencia distinta: así un token de inquilino ni siquiera llega a autenticarse contra
    /// el panel, y el rechazo es 401 en vez de 403.
    /// </summary>
    public const string Operador = "operador";
}

/// <summary>
/// La política que protege el panel del proveedor del SaaS.
///
/// <para><b>Esto no es autorizar por rol</b></para>
/// Es una política sobre un claim, igual que las seis de permisos. La diferencia es que ese
/// claim identifica a la otra identidad del sistema, no a un permiso dentro de una empresa.
///
/// <para><b>Tres barreras, no una</b></para>
/// Un solo <c>RequireClaim</c> bastaría en el papel, pero dejaría toda la separación entre el
/// proveedor y sus clientes colgando de una línea. Se apilan tres comprobaciones
/// independientes: la audiencia del token, el esquema con el que se autentica y la ausencia
/// de claims de tenencia. Para que un inquilino entrara al panel tendrían que fallar las tres.
/// </summary>
public static class PoliticasDeOperador
{
    /// <summary>Nombre de la política. Se anota con <c>.RequireAuthorization(PoliticasDeOperador.Operador)</c>.</summary>
    public const string Operador = "operador";

    public static AuthorizationBuilder AgregarPoliticasDeOperador(this AuthorizationBuilder constructor)
    {
        constructor.AddPolicy(Operador, politica => politica
            .AddAuthenticationSchemes(EsquemasDeAutenticacion.Operador)
            .RequireAuthenticatedUser()
            .RequireClaim(ClavesDeClaim.Operador)
            // Un token de operador no tiene por qué llevar tenencia. Si la lleva, algo lo
            // emitió mal y no se le abre la puerta.
            .RequireAssertion(contexto =>
                !contexto.User.HasClaim(c => c.Type == ClavesDeClaim.Empresa) &&
                !contexto.User.HasClaim(c => c.Type == ClavesDeClaim.Cuenta) &&
                !contexto.User.HasClaim(c => c.Type == ClavesDeClaim.Usuario)));

        return constructor;
    }
}
