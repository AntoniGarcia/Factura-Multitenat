namespace Facturacion.Client.Servicios.Operador;

/// <summary>
/// Políticas del panel en el cliente. Mismos nombres que el Server.
/// Sirven para enrutar y ocultar UI; la protección real la impone el Server.
/// </summary>
public static class PoliticasDelPanel
{
    public const string PoliticaDeOperador = "operador";

    // Permisos individuales (igual que Permisos.Todos del Shared)
    public const string Timbrar = "timbrar";
    public const string Cancelar = "cancelar";
    public const string AdministrarUsuarios = "administrar_usuarios";
    public const string ComprarTimbres = "comprar_timbres";
    public const string VerReportes = "ver_reportes";
    public const string ConfigurarEmpresa = "configurar_empresa";

    public static IReadOnlyList<string> Permisos { get; } =
    [
        Timbrar,
        Cancelar,
        AdministrarUsuarios,
        ComprarTimbres,
        VerReportes,
        ConfigurarEmpresa
    ];
}
