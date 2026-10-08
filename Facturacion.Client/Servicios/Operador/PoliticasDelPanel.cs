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
    public const string AdministrarPaquetes = PermisosDePanel.AdministrarPaquetes;
    public const string CambiarEstadoPaquetes = PermisosDePanel.CambiarEstadoPaquetes;
    public const string VerCompras = PermisosDePanel.VerCompras;
    public const string AcreditarCompras = PermisosDePanel.AcreditarCompras;
    public const string SoloAcreditarCompras = PermisosDePanel.SoloAcreditarCompras;
    public const string DescartarCompras = PermisosDePanel.DescartarCompras;
    public const string VerClientes = PermisosDePanel.VerClientes;
    public const string AdministrarClientes = PermisosDePanel.AdministrarClientes;
    public const string AsignarTimbres = PermisosDePanel.AsignarTimbres;
    public const string EditarContactoCuentas = PermisosDePanel.EditarContactoCuentas;
    public const string CambiarEstadoEmpresas = PermisosDePanel.CambiarEstadoEmpresas;
    public const string GestionarLicencias = PermisosDePanel.GestionarLicencias;
    public const string GestionarMembresias = PermisosDePanel.GestionarMembresias;
    public const string VerUsuarios = PermisosDePanel.VerUsuarios;
    public const string AdministrarUsuarios = PermisosDePanel.AdministrarUsuarios;
    public const string RestablecerContrasenasUsuarios = PermisosDePanel.RestablecerContrasenasUsuarios;
    public const string CambiarCorreosUsuarios = PermisosDePanel.CambiarCorreosUsuarios;
    public const string CambiarEstadoUsuarios = PermisosDePanel.CambiarEstadoUsuarios;
    public const string QuitarAccesoUsuarios = PermisosDePanel.QuitarAccesoUsuarios;
    public const string VerOperadores = PermisosDePanel.VerOperadores;
    public const string AdministrarOperadores = PermisosDePanel.AdministrarOperadores;
    public const string CambiarEstadoOperadores = PermisosDePanel.CambiarEstadoOperadores;
    public const string VerConfiguracion = PermisosDePanel.VerConfiguracion;
    public const string AdministrarConfiguracion = PermisosDePanel.AdministrarConfiguracion;

    public static IReadOnlyList<string> Permisos { get; } = PermisosDePanel.Todos;
}
