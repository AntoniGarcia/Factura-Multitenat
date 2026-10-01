using System.Net;
using System.Net.Mail;

namespace Facturacion.Server.Infra.Correo;

/// <summary>Envío real por SMTP, con la cuenta propia del SaaS (ARQUITECTURA.md §6).</summary>
public sealed class ServicioDeCorreoSmtp(
    IProveedorDeConfiguracionDelSistema configuracion,
    ILogger<ServicioDeCorreoSmtp> registro) : IServicioDeCorreo
{
    public async Task EnviarAsync(MensajeDeCorreo mensaje, CancellationToken ct)
    {
        var config = (await configuracion.ObtenerAsync(ct)).Correo;

        using var cliente = new SmtpClient(config.Servidor, config.Puerto)
        {
            EnableSsl = config.UsarTls,
            Credentials = new NetworkCredential(config.Usuario, config.Contrasena)
        };

        using var correo = new MailMessage
        {
            From = new MailAddress(config.RemitenteCorreo, config.RemitenteNombre),
            Subject = mensaje.Asunto,
            Body = mensaje.CuerpoHtml,
            IsBodyHtml = true
        };

        foreach (var destinatario in mensaje.Para)
            correo.To.Add(destinatario);

        foreach (var oculto in mensaje.CopiaOculta ?? [])
            correo.Bcc.Add(oculto);

        if (!string.IsNullOrWhiteSpace(mensaje.ResponderA))
            correo.ReplyToList.Add(mensaje.ResponderA);

        // Los MemoryStream quedan vivos hasta después de SendMailAsync: Attachment no copia
        // el contenido, lo lee al enviar.
        var flujos = new List<MemoryStream>();

        foreach (var adjunto in mensaje.Adjuntos ?? [])
        {
            var flujo = new MemoryStream(adjunto.Contenido);
            flujos.Add(flujo);
            correo.Attachments.Add(new Attachment(flujo, adjunto.NombreArchivo, adjunto.TipoMime));
        }

        try
        {
            await cliente.SendMailAsync(correo, ct);
        }
        catch (SmtpException excepcion)
        {
            registro.LogError(excepcion, "Falló el envío de correo a los dominios {Dominios}",
                string.Join(", ", mensaje.Para.Select(DominioDe).Distinct()));
            throw;
        }
        finally
        {
            foreach (var flujo in flujos) await flujo.DisposeAsync();
        }
    }

    /// <summary>Solo el dominio: el log no guarda direcciones completas de terceros.</summary>
    private static string DominioDe(string destinatario)
    {
        var separador = destinatario.LastIndexOf('@');
        return separador >= 0 && separador < destinatario.Length - 1
            ? destinatario[(separador + 1)..]
            : "no-disponible";
    }
}
