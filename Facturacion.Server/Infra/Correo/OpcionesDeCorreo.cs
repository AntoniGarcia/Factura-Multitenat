namespace Facturacion.Server.Infra.Correo;

/// <summary>
/// El remitente es del SaaS, no del cliente (CLAUDE.md §6): no se guarda configuración SMTP
/// por empresa, todo sale de esta única cuenta con dominio verificado.
/// <para>
/// Sin <see cref="Servidor"/> configurado, el registro de infraestructura decide qué
/// implementación usar: en <c>Development</c> cae a <see cref="ServicioDeCorreoConsola"/>
/// (no hay por qué exigirle SMTP a quien solo está probando en su máquina); fuera de
/// <c>Development</c>, su ausencia impide arrancar, igual que la clave de firma del JWT.
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
