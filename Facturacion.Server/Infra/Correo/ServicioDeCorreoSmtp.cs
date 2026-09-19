using Facturacion.Server.Infra.Correo;
using Microsoft.Win32;
using System.Net;
using System.Net.Mail;

namespace Facturacion.Server.Infra.Correo;

/// <summary>Envío real por SMTP, con la cuenta propia del SaaS (ARQUITECTURA.md §6).</summary>
public sealed class ServicioDeCorreoSmtp(
    IProveedorDeConfiguracionDelSistema configuracion,
    ILogger<ServicioDeCorreoSmtp> registro) : IServicioDeCorreo
{
    public async Task EnviarAsync(
        string destinatario, string asunto, string cuerpoHtml, CancellationToken ct,
        string? responderA = null, IReadOnlyList<AdjuntoDeCorreo>? adjuntos = null)
    {
        var config = (await configuracion.ObtenerAsync(ct)).Correo;

        using var cliente = new SmtpClient(config.Servidor, config.Puerto)
        {
            EnableSsl = config.UsarTls,
            Credentials = new NetworkCredential(config.Usuario, config.Contrasena)
        };

        using var mensaje = new MailMessage
        {
            From = new MailAddress(config.RemitenteCorreo, config.RemitenteNombre),
            Subject = asunto,
            Body = cuerpoHtml,
            IsBodyHtml = true
        };

        mensaje.To.Add(destinatario);

        if (!string.IsNullOrWhiteSpace(responderA))
            mensaje.ReplyToList.Add(responderA);

        // Los MemoryStream quedan vivos hasta después de SendMailAsync: Attachment no copia
        // el contenido, lo lee al enviar.

        var flujos = new List<MemoryStream>();

        if (adjuntos is { Count: > 0 })
        {
            foreach (var adjunto in adjuntos)
            {
                var flujo = new MemoryStream(adjunto.Contenido);
                flujos.Add(flujo);
                mensaje.Attachments.Add(new Attachment(flujo, adjunto.NombreArchivo, adjunto.TipoMime));
            }
        }

        try
        {
            await cliente.SendMailAsync(mensaje, ct);
        }
        catch (SmtpException excepcion)
        {

            registro.LogError(excepcion, "Falló el envío de correo al dominio {DominioDestinatario}", DominioDe(destinatario));
            throw;
        }
        finally
        {
            foreach (var flujo in flujos) await flujo.DisposeAsync();
        }
    }
        private static string DominioDe(string destinatario)
        {
        var separador = destinatario.LastIndexOf('@');
        return separador >= 0 && separador < destinatario.Length - 1
            ? destinatario[(separador + 1)..]
            : "no-disponible";
        }

}