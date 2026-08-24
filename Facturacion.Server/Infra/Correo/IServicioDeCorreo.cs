namespace Facturacion.Server.Infra.Correo;

/// <summary>
/// Envío de correo del SaaS. Ambas mitades lo consumen: la A para el registro de cuentas,
/// la B para el CFDI, el PDF adjunto y la confirmación de cancelación.
/// </summary>
public interface IServicioDeCorreo
{
    /// <summary>
    /// <paramref name="responderA"/> es del CFDI: el aviso de una factura responde al correo
    /// de la empresa emisora (ARQUITECTURA.md §6), no al buzón del SaaS. El registro no lo usa.
    /// </summary>
    Task EnviarAsync(
        string destinatario, string asunto, string cuerpoHtml, CancellationToken ct, string? responderA = null);
}
