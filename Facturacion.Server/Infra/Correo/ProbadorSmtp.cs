using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace Facturacion.Server.Infra.Correo;

public static class ProbadorSmtp
{
    private static readonly SemaphoreSlim Conexiones = new(4);

    public static ErrorNegocio? Validar(string? servidor, int puerto, string? usuario,
        string? contrasena, string? nombre, string? remitente, bool usarTls = true, bool sslDirecto = true)
    {
        if (string.IsNullOrWhiteSpace(servidor) || !DestinoSmtp.EsNombreValido(servidor.Trim()))
            return ErrorNegocio.Validacion("smtp-servidor-invalido", "Escribe un servidor SMTP válido, sin protocolo ni puerto.");
        if (!PuertosSmtp.EsAdmitido(puerto) || (!sslDirecto && puerto != PuertosSmtp.StartTls))
            return ErrorNegocio.Validacion("smtp-puerto-invalido", sslDirecto
                ? "Usa el puerto 465 (SSL) o 587 (STARTTLS)." : "El correo del sistema usa el puerto 587 con STARTTLS.");
        if (!usarTls)
            return ErrorNegocio.Validacion("smtp-tls-requerido", "Activa la conexión segura.");
        if (string.IsNullOrWhiteSpace(usuario) || usuario.Trim().Length > 254 || TieneSaltos(usuario))
            return ErrorNegocio.Validacion("smtp-usuario-invalido", "Escribe el usuario SMTP en una sola línea (máximo 254 caracteres).");
        if (string.IsNullOrEmpty(contrasena) || contrasena.Length > 512)
            return ErrorNegocio.Validacion("smtp-contrasena-invalida", "Escribe la contraseña SMTP o conserva una guardada (máximo 512 caracteres).");
        if (nombre is not null && (nombre.Trim().Length > 128 || TieneSaltos(nombre)))
            return ErrorNegocio.Validacion("smtp-nombre-invalido", "El nombre del remitente admite hasta 128 caracteres en una sola línea.");
        if (string.IsNullOrWhiteSpace(remitente) || remitente.Trim().Length > 254 || TieneSaltos(remitente)
            || !MailAddress.TryCreate(remitente.Trim(), out var direccion) || direccion.Address != remitente.Trim())
            return ErrorNegocio.Validacion("smtp-remitente-invalido", "Escribe solo el correo del remitente, sin nombres ni ángulos.");
        return null;
    }

    public static async Task<Resultado<bool>> ProbarAsync(string servidor, int puerto, string usuario,
        string contrasena, CancellationToken ct)
    {
        if (!await Conexiones.WaitAsync(0, ct))
            return ErrorNegocio.Conflicto("smtp-prueba-ocupada", "Hay otras pruebas de correo en curso. Vuelve a intentar.");
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeSpan.FromSeconds(15));
            var destino = await DestinoSmtp.ResolverAsync(servidor, limite.Token);
            if (destino.EsFallo) return destino.Error!;
            var ip = destino.Valor;
            using var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            using var cliente = new MailKit.Net.Smtp.SmtpClient { Timeout = 15000 };
            await socket.ConnectAsync(new IPEndPoint(ip, puerto), limite.Token);
            // TLS verifica el nombre original; la conexión usa la IP pública ya validada.
            await cliente.ConnectAsync(socket, servidor, puerto,
                puerto == PuertosSmtp.SslDirecto ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, limite.Token);
            await cliente.AuthenticateAsync(usuario, contrasena, limite.Token);
            await cliente.DisconnectAsync(true, limite.Token);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return ErrorNegocio.Validacion("smtp-tiempo-agotado", "El servidor SMTP no respondió a tiempo. Revisa servidor y puerto.");
        }
        catch (MailKit.Security.AuthenticationException)
        {
            return ErrorNegocio.Validacion("smtp-autenticacion", "El servidor rechazó el usuario o la contraseña. En Gmail usa una contraseña de aplicación.");
        }
        catch (SslHandshakeException)
        {
            return ErrorNegocio.Validacion("smtp-tls", "No se pudo establecer la conexión segura. Revisa el puerto y el certificado SMTP.");
        }
        catch (Exception error) when (error is SocketException or IOException or TimeoutException
            or SmtpCommandException or SmtpProtocolException or NotSupportedException
            or System.Security.Authentication.AuthenticationException or MailKit.ServiceNotConnectedException)
        {
            return ErrorNegocio.Validacion("smtp-conexion", "No se pudo comprobar el SMTP. Revisa servidor, puerto y acceso de red.");
        }
        finally { Conexiones.Release(); }
    }

    private static bool TieneSaltos(string texto) => texto.Contains('\r') || texto.Contains('\n');
}
