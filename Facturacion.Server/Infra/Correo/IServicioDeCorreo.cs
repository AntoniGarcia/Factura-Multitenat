namespace Facturacion.Server.Infra.Correo;

/// <summary>
/// Envío de correo del SaaS. Ambas mitades lo consumen: la A para el registro de cuentas,
/// la B para el CFDI, el PDF adjunto y la confirmación de cancelación.
/// </summary>

public sealed record AdjuntoDeCorreo(string NombreArchivo, byte[] Contenido, string TipoMime);

/// <param name="ResponderA">
/// Es del CFDI: el aviso de una factura responde al correo de la empresa emisora
/// (ARQUITECTURA.md §6), no al buzón del SaaS. El registro no lo usa.
/// </param>
/// <param name="NombreRemitente">
/// Nombre que acompaña a la dirección del sistema en vez del suyo. Lo pone la empresa para sus
/// comprobantes; el registro y los demás correos del sistema no lo usan.
/// </param>
/// <param name="CopiaOculta">
/// La copia a la cuenta de la empresa va oculta: el cliente no tiene por qué ver a dónde
/// archiva su proveedor lo que le manda.
/// </param>
public sealed record MensajeDeCorreo(
    IReadOnlyList<string> Para,
    string Asunto,
    string CuerpoHtml,
    string? ResponderA = null,
    IReadOnlyList<string>? CopiaOculta = null,
    IReadOnlyList<AdjuntoDeCorreo>? Adjuntos = null,
    string? NombreRemitente = null);

public interface IServicioDeCorreo
{
    Task EnviarAsync(MensajeDeCorreo mensaje, CancellationToken ct);
}
