using System.Net;

namespace Facturacion.Server.Infra.Instalacion;

public static class AccesoAlAsistente
{
    public static bool SinClaveLocal(IConfiguration configuracion, IHostEnvironment entorno, HttpContext contexto)
    {
        if (!entorno.IsStaging() || !configuracion.GetValue<bool>("Instalacion:PermitirSinClaveLocal")) return false;
        var ip = contexto.Connection.RemoteIpAddress;
        if (ip is null || !IPAddress.IsLoopback(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip)) return false;
        var host = contexto.Request.Host.Host.Trim('[', ']');
        if (!host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            && !(IPAddress.TryParse(host, out var destino) && IPAddress.IsLoopback(destino))) return false;
        // Impide que una página ajena invoque el asistente local desde el navegador.
        var sitio = contexto.Request.Headers["Sec-Fetch-Site"];
        if (sitio.Count > 0 && sitio.ToString() is not ("same-origin" or "none")) return false;
        if (!contexto.Request.Headers.TryGetValue("Origin", out var origen)) return true;
        return origen.Count == 1 && Uri.TryCreate(origen.ToString(), UriKind.Absolute, out var uri)
            && Uri.TryCreate($"{contexto.Request.Scheme}://{contexto.Request.Host}", UriKind.Absolute, out var actual)
            && Uri.Compare(uri, actual, UriComponents.SchemeAndServer, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
    }

    public static bool Autoriza(IConfiguration configuracion, IHostEnvironment entorno, HttpContext contexto, ConfiguracionDeInstalacion archivo)
        => SinClaveLocal(configuracion, entorno, contexto)
            || archivo.Autoriza(contexto.Request.Headers["X-Clave-Instalacion"].ToString());
}
