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
/// El operador principal (el que crea la BD) se siembra con todos los permisos del panel.
/// Si algún día hace falta distinguir, se añade el permiso y no un rol.
/// </summary>
public static class PoliticasDeOperador
{
    /// <summary>Política base: cualquier operador autenticado. Se usa en el grupo de rutas.</summary>
    public const string Operador = "operador";

    /// <summary>Permisos individuales del panel — mismas claves que <see cref="PermisosDePanel"/>.</summary>
    public const string VerPaquetes = PermisosDePanel.VerPaquetes;
    public const string VerCompras = PermisosDePanel.VerCompras;
    public const string SoloAcreditarCompras = PermisosDePanel.SoloAcreditarCompras;
    public const string DescartarCompras = PermisosDePanel.DescartarCompras;
    public const string FacturarCompras = PermisosDePanel.FacturarCompras;
    public const string TimbrarCompras = PermisosDePanel.TimbrarCompras;
    public const string VerClientes = PermisosDePanel.VerClientes;
    public const string EditarContactoCuentas = PermisosDePanel.EditarContactoCuentas;
    public const string GestionarLicencias = PermisosDePanel.GestionarLicencias;
    public const string GestionarMembresias = PermisosDePanel.GestionarMembresias;
    public const string GestionarLimites = PermisosDePanel.GestionarLimites;
    public const string VerUsuarios = PermisosDePanel.VerUsuarios;
    public const string RestablecerContrasenasUsuarios = PermisosDePanel.RestablecerContrasenasUsuarios;
    public const string CambiarCorreosUsuarios = PermisosDePanel.CambiarCorreosUsuarios;
    public const string QuitarAccesoUsuarios = PermisosDePanel.QuitarAccesoUsuarios;
    public const string VerOperadores = PermisosDePanel.VerOperadores;
    public const string VerConfiguracion = PermisosDePanel.VerConfiguracion;
    public const string VerCatalogosSat = PermisosDePanel.VerCatalogosSat;

    /// <summary>Todas las políticas de permiso, para iterar si se necesita.</summary>
    public const string CrearPaquetes = PermisosDePanel.CrearPaquetes;
    public const string EditarPaquetes = PermisosDePanel.EditarPaquetes;
    public const string EliminarPaquetes = PermisosDePanel.EliminarPaquetes;
    public const string RetirarPaquetes = PermisosDePanel.RetirarPaquetes;
    public const string ReactivarPaquetes = PermisosDePanel.ReactivarPaquetes;
    public const string DesactivarEmpresas = PermisosDePanel.DesactivarEmpresas;
    public const string ReactivarEmpresas = PermisosDePanel.ReactivarEmpresas;
    public const string CrearPaquetesExclusivos = PermisosDePanel.CrearPaquetesExclusivos;
    public const string EditarPaquetesExclusivos = PermisosDePanel.EditarPaquetesExclusivos;
    public const string DesactivarPaquetesExclusivos = PermisosDePanel.DesactivarPaquetesExclusivos;
    public const string ReactivarPaquetesExclusivos = PermisosDePanel.ReactivarPaquetesExclusivos;
    public const string DesactivarUsuarios = PermisosDePanel.DesactivarUsuarios;
    public const string ReactivarUsuarios = PermisosDePanel.ReactivarUsuarios;
    public const string CrearOperadores = PermisosDePanel.CrearOperadores;
    public const string EditarOperadores = PermisosDePanel.EditarOperadores;
    public const string EliminarOperadores = PermisosDePanel.EliminarOperadores;
    public const string DesactivarOperadores = PermisosDePanel.DesactivarOperadores;
    public const string ReactivarOperadores = PermisosDePanel.ReactivarOperadores;
    public const string CambiarCorreoOperadores = PermisosDePanel.CambiarCorreoOperadores;
    public const string CambiarContrasenaOperadores = PermisosDePanel.CambiarContrasenaOperadores;
    public const string VerActividadOperadores = PermisosDePanel.VerActividadOperadores;
    public const string ActualizarCatalogosSat = PermisosDePanel.ActualizarCatalogosSat;
    public const string EditarCorreoSistema = PermisosDePanel.EditarCorreoSistema;
    public const string ProbarCorreoSistema = PermisosDePanel.ProbarCorreoSistema;
    public const string EditarDatosFiscalesProveedor = PermisosDePanel.EditarDatosFiscalesProveedor;
    public const string CargarCertificadoProveedor = PermisosDePanel.CargarCertificadoProveedor;
    public const string AbrirFormularioPaquetes = PermisosDePanel.AbrirFormularioPaquetes;

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

        // El token ya trae las claves concretas, incluidas las convertidas de asignaciones anteriores.
        foreach (var permiso in Permisos)
        {
            constructor.AddPolicy(permiso, politica => politica
                .AddAuthenticationSchemes(EsquemasDeAutenticacion.Operador)
                .RequireAuthenticatedUser()
                .RequireClaim(ClavesDeClaim.Operador)
                .RequireAssertion(contexto =>
                    PermisosDePanel.Autoriza(
                        contexto.User.FindAll(ClavesDeClaim.Permiso).Select(c => c.Value), permiso) &&
                    !contexto.User.HasClaim(c => c.Type == ClavesDeClaim.Empresa) &&
                    !contexto.User.HasClaim(c => c.Type == ClavesDeClaim.Cuenta) &&
                    !contexto.User.HasClaim(c => c.Type == ClavesDeClaim.Usuario)));
        }

        return constructor;
    }
}
