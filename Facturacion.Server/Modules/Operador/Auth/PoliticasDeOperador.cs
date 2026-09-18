using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
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
/// Políticas de autorización para el panel del proveedor del SaaS.
///
/// <para><b>Dos niveles</b></para>
/// 1. <see cref="Operador"/> — base: cualquier operador autenticado (claim "opr").
/// 2. Una por permiso (<c>perm:panel_ver_paquetes</c>, etc.) — por sección, con su ver y
///    su administrar. Vocabulario propio del panel (<see cref="PermisosDePanel"/>), no el de
///    los inquilinos: el operador no tiene empresa ni bolsa.
///
/// <para><b>Por qué no hay "superadmin"</b></para>
/// El operador principal (el que crea la BD) se siembra con los trece permisos del panel.
/// Si algún día hace falta distinguir, se añade el permiso y no un rol.
/// </summary>
public static class PoliticasDeOperador
{
    /// <summary>Política base: cualquier operador autenticado. Se usa en el grupo de rutas.</summary>
    public const string Operador = "operador";

    /// <summary>Permisos individuales del panel — mismas claves que <see cref="PermisosDePanel"/>.</summary>
    public const string VerPaquetes = PermisosDePanel.VerPaquetes;
    public const string AdministrarPaquetes = PermisosDePanel.AdministrarPaquetes;
    public const string VerCompras = PermisosDePanel.VerCompras;
    public const string AcreditarCompras = PermisosDePanel.AcreditarCompras;
    public const string VerClientes = PermisosDePanel.VerClientes;
    public const string AdministrarClientes = PermisosDePanel.AdministrarClientes;
    public const string AsignarTimbres = PermisosDePanel.AsignarTimbres;
    public const string VerUsuarios = PermisosDePanel.VerUsuarios;
    public const string AdministrarUsuarios = PermisosDePanel.AdministrarUsuarios;
    public const string VerOperadores = PermisosDePanel.VerOperadores;
    public const string AdministrarOperadores = PermisosDePanel.AdministrarOperadores;
    public const string VerConfiguracion = PermisosDePanel.VerConfiguracion;
    public const string AdministrarConfiguracion = PermisosDePanel.AdministrarConfiguracion;

    /// <summary>Todas las políticas de permiso, para iterar si se necesita.</summary>
    public static IReadOnlyList<string> Permisos { get; } = PermisosDePanel.Todos;

    public static AuthorizationBuilder AgregarPoliticasDeOperador(this AuthorizationBuilder constructor)
    {
        // Base: operador autenticado, audiencia correcta, sin claims de tenencia.
        constructor.AddPolicy(Operador, politica => politica
            .AddAuthenticationSchemes(EsquemasDeAutenticacion.Operador)
            .RequireAuthenticatedUser()
            .RequireClaim(ClavesDeClaim.Operador)
            .RequireAssertion(contexto =>
                !contexto.User.HasClaim(c => c.Type == ClavesDeClaim.Empresa) &&
                !contexto.User.HasClaim(c => c.Type == ClavesDeClaim.Cuenta) &&
                !contexto.User.HasClaim(c => c.Type == ClavesDeClaim.Usuario)));

        // Una política por permiso: RequireClaim("perm", "panel_ver_paquetes") etc.
        foreach (var permiso in Permisos)
        {
            constructor.AddPolicy(permiso, politica => politica
                .AddAuthenticationSchemes(EsquemasDeAutenticacion.Operador)
                .RequireAuthenticatedUser()
                .RequireClaim(ClavesDeClaim.Operador)
                .RequireClaim(ClavesDeClaim.Permiso, permiso)
                .RequireAssertion(contexto =>
                    !contexto.User.HasClaim(c => c.Type == ClavesDeClaim.Empresa) &&
                    !contexto.User.HasClaim(c => c.Type == ClavesDeClaim.Cuenta) &&
                    !contexto.User.HasClaim(c => c.Type == ClavesDeClaim.Usuario)));
        }

        return constructor;
    }
}
