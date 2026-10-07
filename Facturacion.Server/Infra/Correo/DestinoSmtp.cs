using System.Net;
using System.Net.Sockets;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Infra.Correo;

/// <summary>
/// Decide si el servidor SMTP que capturó una empresa es un destino al que este servidor puede
/// conectarse (AGENTS.md §11).
///
/// <para><b>Por qué</b></para>
/// El servidor lo escribe el usuario. Sin esta revisión, la aplicación se conectaría adonde le
/// digan: su propia red interna, el servicio de metadatos de Azure, otro equipo de la VNet.
///
/// <para><b>Resolver una sola vez y conectar a esa IP</b></para>
/// Si se validara el nombre y después el cliente SMTP lo resolviera de nuevo, un DNS bajo
/// control de un atacante podría contestar una IP pública a la validación y una privada a la
/// conexión. Por eso devuelve la dirección ya validada, y la conexión va a ella.
/// </summary>
public static class DestinoSmtp
{
    public const int LongitudMaxima = 253;

    public static bool EsNombreValido(string servidor)
        => servidor.Length <= LongitudMaxima &&
           Uri.CheckHostName(servidor) is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6;

    public static async Task<Resultado<IPAddress>> ResolverAsync(string servidor, CancellationToken ct)
    {
        if (!EsNombreValido(servidor))
            return ErrorNegocio.Validacion("smtp-servidor-invalido", "El servidor SMTP no tiene un formato válido.");

        IPAddress[] direcciones;

        if (IPAddress.TryParse(servidor, out var literal))
            direcciones = [literal];
        else
        {
            try
            {
                direcciones = await Dns.GetHostAddressesAsync(servidor, ct);
            }
            catch (SocketException)
            {
                direcciones = [];
            }
        }

        if (direcciones.Length == 0)
            return ErrorNegocio.Validacion("smtp-servidor-no-encontrado",
                $"No se encontró el servidor «{servidor}». Revisa que esté bien escrito.");

        // Basta una dirección no permitida para rechazar el nombre entero: un registro con una
        // IP pública y otra privada es justo la forma de colar la segunda.
        if (direcciones.Any(d => !EsPublica(d)))
            return ErrorNegocio.Validacion("smtp-servidor-no-permitido",
                "Ese servidor apunta a una red privada o local. Usa el servidor público de tu proveedor de correo.");

        return Resultado<IPAddress>.Exito(
            direcciones.FirstOrDefault(d => d.AddressFamily == AddressFamily.InterNetwork) ?? direcciones[0]);
    }

    private static bool EsPublica(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip))
            return false;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();

            return !(b[0] is 0 or 10 or 127 ||
                     b[0] >= 224 ||                                  // multicast y reservadas
                     b[0] == 100 && b[1] is >= 64 and <= 127 ||       // CGNAT
                     b[0] == 169 && b[1] == 254 ||                    // link-local y metadatos de la nube
                     b[0] == 172 && b[1] is >= 16 and <= 31 ||
                     b[0] == 192 && b[1] == 168 ||
                     b[0] == 192 && b[1] == 0 && b[2] is 0 or 2 ||
                     b[0] == 198 && b[1] is 18 or 19 ||
                     b[0] == 198 && b[1] == 51 && b[2] == 100 ||
                     b[0] == 203 && b[1] == 0 && b[2] == 113);
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.Equals(IPAddress.IPv6Any) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal ||
                ip.IsIPv6Multicast || ip.IsIPv6UniqueLocal || ip.IsIPv6Teredo)
                return false;

            var b = ip.GetAddressBytes();

            // 64:ff9b::/96 lleva una IPv4 dentro (NAT64) y 2001:db8::/32 es de documentación.
            return !(b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b ||
                     b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8);
        }

        return false;
    }
}
