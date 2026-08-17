using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Facturacion.Server.Infra.Correo;

/// <summary>Envío real por SMTP, con la cuenta propia del SaaS (CLAUDE.md §6).</summary>
public sealed class ServicioDeCorreoSmtp(
    IOptions<OpcionesDeCorreo> opciones, ILogger<ServicioDeCorreoSmtp> registro) : IServicioDeCorreo
{
    private readonly OpcionesDeCorreo _opciones = opciones.Value;

    public async Task EnviarAsync(
        string destinatario, string asunto, string cuerpoHtml, CancellationToken ct, string? responderA = null)
    {
        using var cliente = new SmtpClient(_opciones.Servidor, _opciones.Puerto)
        {
            EnableSsl = _opciones.UsarTls,
            Credentials = new NetworkCredential(_opciones.Usuario, _opciones.Contrasena)
        };

        using var mensaje = new MailMessage
        {
            From = new MailAddress(_opciones.RemitenteCorreo, _opciones.RemitenteNombre),
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
            // caído, y la invitación ya quedó guardada. Se registra para que el operador lo vea.
            registro.LogError(excepcion, "Falló el envío de correo a {Destinatario}", destinatario);
            throw;
        }
    }
}
