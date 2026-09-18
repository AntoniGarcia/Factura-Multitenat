namespace Facturacion.Server.Infra.Correo;

/// <summary>
/// El remitente es del SaaS, no del cliente (ARQUITECTURA.md §6): no se guarda configuración SMTP
/// por empresa, todo sale de esta única cuenta con dominio verificado.
/// <para>
/// Estos valores son la configuración inicial. Cuando el operador guarda cambios, la versión
/// persistida en SQL Server pasa a ser la efectiva y la contraseña queda cifrada.
/// </para>
/// </summary>
public sealed class OpcionesDeCorreo
{
    public const string Seccion = "Correo";

    public string Servidor { get; init; } = string.Empty;

    public int Puerto { get; init; } = 587;

    public string Usuario { get; init; } = string.Empty;

    public string Contrasena { get; init; } = string.Empty;

    /// <summary>Dirección que ve el destinatario. El dominio debe tener SPF, DKIM y DMARC.</summary>
    public string RemitenteCorreo { get; init; } = string.Empty;

    public string RemitenteNombre { get; init; } = "Sistema de facturación";

    public bool UsarTls { get; init; } = true;
}
