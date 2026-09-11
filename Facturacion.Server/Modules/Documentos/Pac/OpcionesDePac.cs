namespace Facturacion.Server.Modules.Documentos.Pac;

/// <summary>
/// Credenciales y ambiente del PAC.
///
/// <para><b>Nunca en el repositorio</b></para>
/// Igual que la clave de firma del JWT y la llave maestra del almacén: van en
/// <c>appsettings.Development.json</c> —gitignoreado— en la máquina de desarrollo, y en
/// variables de entorno en producción (ARQUITECTURA.md §4).
///
/// <para><b>Ambiente de pruebas y de producción son cuentas distintas</b></para>
/// El sandbox de SW no comparte usuario con producción, así que cambiar de uno a otro es
/// cambiar <see cref="UrlBase"/> <b>y</b> las credenciales. Se separan a propósito en vez de
/// deducir la URL de un interruptor: un despliegue que timbra contra el sandbox creyendo que
/// es producción emite comprobantes que el SAT no reconoce.
/// </para>
/// </summary>
public sealed class OpcionesDePac
{
    public const string Seccion = "Pac";

    /// <summary>
    /// <c>Deshabilitado</c> permite levantar un entorno de revisión sin credenciales y sin
    /// simular documentos fiscales. <c>Real</c> exige la configuración completa del PAC.
    /// </summary>
    public ModoDePac Modo { get; init; } = ModoDePac.Real;

    /// <summary>Sandbox: <c>https://services.test.sw.com.mx</c>. Producción: <c>https://services.sw.com.mx</c>.</summary>
    public string UrlBase { get; init; } = string.Empty;

    /// <summary>Correo de la cuenta del PAC.</summary>
    public string Usuario { get; init; } = string.Empty;

    public string Contrasena { get; init; } = string.Empty;

    /// <summary>
    /// Cuánto esperar al PAC antes de darlo por incomunicado. Generoso a propósito: un
    /// timbrado lento que termina bien es mejor que uno abortado que deja el comprobante en
    /// el limbo y obliga a conciliar.
    /// </summary>
    public int TiempoDeEsperaSegundos { get; init; } = 60;

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(UrlBase) &&
        !string.IsNullOrWhiteSpace(Usuario) &&
        !string.IsNullOrWhiteSpace(Contrasena);
}

public enum ModoDePac
{
    Real,
    Deshabilitado
}
