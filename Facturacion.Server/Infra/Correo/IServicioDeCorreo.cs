namespace Facturacion.Server.Infra.Correo;

/// <summary>
/// Envío de correo del SaaS. Ambas mitades lo consumen: la A para invitaciones, la B para
/// el CFDI, el PDF adjunto y la confirmación de cancelación.
/// </summary>
public interface IServicioDeCorreo
{
    /// <summary>
    /// <paramref name="responderA"/> es del CFDI: el aviso de una factura responde al correo
    /// de la empresa emisora (CLAUDE.md §6), no al buzón del SaaS. Las invitaciones no lo usan.
    /// </summary>
    Task EnviarAsync(
        string destinatario, string asunto, string cuerpoHtml, CancellationToken ct, string? responderA = null);
}
