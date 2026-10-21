namespace Facturacion.Server.Infra.Correo;

/// <summary>
/// Configuración inicial del correo del sistema. Los cambios del operador se guardan en SQL con la contraseña cifrada.
/// El SMTP propio de cada empresa se configura por separado para sus comprobantes.
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
