namespace Facturacion.Server.Infra.Correo;

// Correo del SaaS: registro, contraseñas e invitaciones. Los comprobantes salen por
// IServicioDeCorreoDeEmpresa.

public sealed record AdjuntoDeCorreo(string NombreArchivo, byte[] Contenido, string TipoMime);

// ResponderA: en un CFDI, el correo de la empresa emisora. CopiaOculta: el cliente no ve a
// dónde archiva su proveedor lo que le manda.
public sealed record MensajeDeCorreo(
    IReadOnlyList<string> Para,
    string Asunto,
    string CuerpoHtml,
    string? ResponderA = null,
    IReadOnlyList<string>? CopiaOculta = null,
    IReadOnlyList<AdjuntoDeCorreo>? Adjuntos = null,
    bool EsConfidencial = false);

public interface IServicioDeCorreo
{
    Task EnviarAsync(MensajeDeCorreo mensaje, CancellationToken ct);
}
