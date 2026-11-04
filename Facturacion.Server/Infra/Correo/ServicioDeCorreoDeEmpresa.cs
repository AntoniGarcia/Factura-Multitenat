using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Plataforma;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace Facturacion.Server.Infra.Correo;

/// <summary>
/// Envío de los correos de una empresa: sus comprobantes. Los correos del sistema —registro,
/// contraseñas, invitaciones— no pasan por aquí: usan <see cref="IServicioDeCorreo"/> directo.
/// </summary>
public interface IServicioDeCorreoDeEmpresa
{
    /// <summary>
    /// Envía con el SMTP de la empresa activa, el único camino para sus comprobantes. Si no lo
    /// tiene configurado, o si su servidor falla, lanza <see cref="FalloDeCorreoDeEmpresa"/>:
    /// nunca se manda por el del SaaS.
    /// </summary>
    Task EnviarAsync(MensajeDeCorreo mensaje, CancellationToken ct);
}

/// <summary>
/// El servidor de correo de la empresa rechazó o no completó el envío. <see cref="Exception.Message"/>
/// ya está redactado para el usuario: no trae la respuesta cruda del servidor.
/// </summary>
public sealed class FalloDeCorreoDeEmpresa(string codigo, string mensaje, Exception? causa = null)
    : Exception(mensaje, causa)
{
    public string Codigo { get; } = codigo;
}

public sealed class ServicioDeCorreoDeEmpresa(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno contexto,
    IProtectorDeContrasenaSmtp protector,
    ILogger<ServicioDeCorreoDeEmpresa> registro) : IServicioDeCorreoDeEmpresa
{
    /// <summary>Tope para conectar y para cada operación con el servidor de la empresa.</summary>
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(15);

    /// <summary>Tiene lo necesario para conectar y firmar el remitente.</summary>
    public static bool EstaCompleto(CorreoDeEmpresa? config)
        => config is { Servidor.Length: > 0, Usuario.Length: > 0, ContrasenaCifrada.Length: > 0, RemitenteCorreo.Length: > 0 };

    public async Task EnviarAsync(MensajeDeCorreo mensaje, CancellationToken ct)
    {
        var config = await baseDeDatos.CorreosDeEmpresa.AsNoTracking().FirstOrDefaultAsync(ct);

        if (contexto.EmpresaActual is not { } empresa ||
            config is not { Servidor: { Length: > 0 } servidor, Usuario: { Length: > 0 } usuario,
                            ContrasenaCifrada: { Length: > 0 } cifrada, RemitenteCorreo: { Length: > 0 } remitente })
            throw new FalloDeCorreoDeEmpresa("smtp-sin-configurar",
                "Esta empresa no tiene configurado su servidor de correo, y sin él no se envían comprobantes.");

        // El nombre se vuelve a resolver en cada envío: el DNS pudo cambiar desde que se guardó.
        var destino = await DestinoSmtp.ResolverAsync(servidor, ct);
        if (destino.EsFallo)
            throw new FalloDeCorreoDeEmpresa(destino.Error!.Codigo, destino.Error.Mensaje);

        string contrasena;

        try
        {
            contrasena = protector.Descifrar(empresa, cifrada);
        }
        catch (CryptographicException ex)
        {
            registro.LogError(ex, "No se pudo descifrar la contraseña SMTP de la empresa {Empresa}.", empresa);
            throw new FalloDeCorreoDeEmpresa("smtp-propio-contrasena-ilegible",
                "No se pudo leer la contraseña guardada de tu servidor de correo. Captúrala de nuevo.");
        }

        using var correo = Armar(mensaje, new MailboxAddress(config.RemitenteNombre ?? string.Empty, remitente));
        await EnviarPorSmtpAsync(correo, servidor, config.Puerto, destino.Valor, usuario, contrasena, ct);
    }

    private async Task EnviarPorSmtpAsync(
        MimeMessage correo, string servidor, int puerto, IPAddress ip, string usuario, string contrasena,
        CancellationToken ct)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(Limite);

        using var cliente = new SmtpClient { Timeout = (int)Limite.TotalMilliseconds };
        using var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        var seguridad = puerto == PuertosSmtp.SslDirecto
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

        try
        {
            // Se conecta el socket a la IP ya validada y MailKit solo hace TLS sobre él. El
            // nombre viaja para la validación del certificado, no para resolverse otra vez.
            await socket.ConnectAsync(new IPEndPoint(ip, puerto), limite.Token);
            await cliente.ConnectAsync(socket, servidor, puerto, seguridad, limite.Token);
            await cliente.AuthenticateAsync(usuario, contrasena, limite.Token);
            await cliente.SendAsync(correo, limite.Token);
            await cliente.DisconnectAsync(true, limite.Token);
        }
        catch (Exception ex) when (ex is TimeoutException ||
                                   ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw Fallo(ex, "smtp-propio-tiempo-agotado",
                "Tu servidor de correo no respondió a tiempo. Revisa el servidor y el puerto.");
        }
        catch (SocketException ex)
        {
            throw Fallo(ex, "smtp-propio-sin-conexion",
                "No se pudo conectar con tu servidor de correo. Revisa el servidor y el puerto.");
        }
        catch (SslHandshakeException ex)
        {
            throw Fallo(ex, "smtp-propio-tls",
                "No se pudo establecer una conexión cifrada con tu servidor de correo. Revisa el puerto: " +
                "465 usa SSL directo y 587 usa STARTTLS.");
        }
        catch (MailKit.Security.AuthenticationException ex)
        {
            throw Fallo(ex, "smtp-propio-autenticacion",
                "Tu servidor de correo rechazó el usuario o la contraseña. Algunos proveedores, como Gmail " +
                "o Outlook, exigen una contraseña de aplicación.");
        }
        catch (SmtpCommandException ex)
        {
            // Sin la excepción completa: su mensaje puede traer la dirección rechazada, y el log
            // no guarda direcciones de terceros.
            registro.LogWarning("El SMTP de la empresa rechazó el envío: {Codigo} {Estado}.",
                ex.ErrorCode, (int)ex.StatusCode);

            throw ex.ErrorCode switch
            {
                SmtpErrorCode.SenderNotAccepted => new FalloDeCorreoDeEmpresa("smtp-propio-remitente",
                    "Tu servidor de correo no aceptó el remitente. Usa como remitente la misma cuenta del usuario."),
                SmtpErrorCode.RecipientNotAccepted => new FalloDeCorreoDeEmpresa("smtp-propio-destinatario",
                    "Tu servidor de correo no aceptó alguno de los destinatarios."),
                _ => new FalloDeCorreoDeEmpresa("smtp-propio-rechazado",
                    "Tu servidor de correo rechazó el mensaje.")
            };
        }
        catch (Exception ex) when (ex is SmtpProtocolException or IOException or NotSupportedException
                                       or ServiceNotConnectedException or ServiceNotAuthenticatedException)
        {
            throw Fallo(ex, "smtp-propio-error",
                "Tu servidor de correo cortó la comunicación. Revisa el servidor, el puerto y el cifrado.");
        }
    }

    private FalloDeCorreoDeEmpresa Fallo(Exception causa, string codigo, string mensaje)
    {
        registro.LogWarning(causa, "Falló el envío por el SMTP de la empresa: {Codigo}.", codigo);
        return new FalloDeCorreoDeEmpresa(codigo, mensaje, causa);
    }

    private static MimeMessage Armar(MensajeDeCorreo mensaje, MailboxAddress remitente)
    {
        var correo = new MimeMessage();
        correo.From.Add(remitente);

        foreach (var destinatario in mensaje.Para)
            correo.To.Add(MailboxAddress.Parse(destinatario));

        foreach (var oculto in mensaje.CopiaOculta ?? [])
            correo.Bcc.Add(MailboxAddress.Parse(oculto));

        if (!string.IsNullOrWhiteSpace(mensaje.ResponderA))
            correo.ReplyTo.Add(MailboxAddress.Parse(mensaje.ResponderA));

        correo.Subject = mensaje.Asunto;

        var cuerpo = new BodyBuilder { HtmlBody = mensaje.CuerpoHtml };

        foreach (var adjunto in mensaje.Adjuntos ?? [])
            cuerpo.Attachments.Add(adjunto.NombreArchivo, adjunto.Contenido, ContentType.Parse(adjunto.TipoMime));

        correo.Body = cuerpo.ToMessageBody();
        return correo;
    }
}
