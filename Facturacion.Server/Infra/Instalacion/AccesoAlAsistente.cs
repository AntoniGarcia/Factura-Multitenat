using System.Net;

namespace Facturacion.Server.Infra.Instalacion;

public static class AccesoAlAsistente
{
    public const string ClaveAccesoRemotoRevision = "Instalacion:PermitirAccesoRemotoRevision";

    public static bool Autoriza(IHostEnvironment entorno, HttpContext contexto, IConfiguration? configuracion = null)
    {
        var permiteRemoto = entorno.IsStaging()
            && configuracion?.GetValue<bool>(ClaveAccesoRemotoRevision) == true;

        if (!permiteRemoto)
        {
            var ip = contexto.Connection.RemoteIpAddress;
            if (ip is null || !IPAddress.IsLoopback(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip)) return false;
        }
        // No permite invocar el asistente desde una página de otro origen.
        var sitio = contexto.Request.Headers["Sec-Fetch-Site"];
        if (sitio.Count > 0 && sitio.ToString() is not ("same-origin" or "none")) return false;
        if (!contexto.Request.Headers.TryGetValue("Origin", out var origen)) return true;
        return origen.Count == 1 && Uri.TryCreate(origen.ToString(), UriKind.Absolute, out var uri)
            && Uri.TryCreate($"{contexto.Request.Scheme}://{contexto.Request.Host}", UriKind.Absolute, out var actual)
            && Uri.Compare(uri, actual, UriComponents.SchemeAndServer, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
    }

}
