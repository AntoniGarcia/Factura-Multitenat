using System.Net;
using System.Net.Mail;

namespace Facturacion.Server.Infra.Correo;

/// <summary>Envío real por SMTP, con la cuenta propia del SaaS (ARQUITECTURA.md §6).</summary>
public sealed class ServicioDeCorreoSmtp(
    IProveedorDeConfiguracionDelSistema configuracion,
    ILogger<ServicioDeCorreoSmtp> registro) : IServicioDeCorreo
{
    public async Task EnviarAsync(
        string destinatario, string asunto, string cuerpoHtml, CancellationToken ct, string? responderA = null)
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

        try
        {
            // SmtpClient no tiene una sobrecarga que acepte CancellationToken; se registra
            // el intento de cancelación pero el envío en curso no se puede abortar a medias.
            await cliente.SendMailAsync(mensaje, ct);
        }
        catch (SmtpException excepcion)
        {
            // No se relanza como error de negocio: quien invita no puede corregir un SMTP
            // caído, y lo que lo disparó ya quedó guardado. Se registra para que el operador lo vea.
            registro.LogError(
                excepcion,
                "Falló el envío de correo al dominio {DominioDestinatario}",
                DominioDe(destinatario));
            throw;
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
