using Facturacion.Shared.Operador;

namespace Facturacion.Client.Servicios.Operador;

/// <summary>
/// Políticas del panel en el cliente. Mismos nombres que el Server.
/// Sirven para enrutar y ocultar UI; la protección real la impone el Server.
/// </summary>
public static class PoliticasDelPanel
{
    public const string PoliticaDeOperador = "operador";

    // Permisos individuales del panel (igual que PermisosDePanel.Todos del Shared)
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
}
