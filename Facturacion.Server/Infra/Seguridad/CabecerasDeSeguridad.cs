namespace Facturacion.Server.Infra.Seguridad;

/// <summary>
/// Cabeceras de seguridad en todas las respuestas (CLAUDE.md §4).
/// <para>
/// La política de contenido la arma <see cref="PoliticaDeContenido"/> una sola vez, al
/// construir la tubería. No hay <c>unsafe-inline</c> ni <c>unsafe-eval</c>: si algo deja de
/// funcionar por la CSP, se corrige ese algo, no se afloja la política.
/// </para>
/// </summary>
public sealed class CabecerasDeSeguridad(RequestDelegate siguiente, string politicaDeContenido)
{
    private const string PoliticaPermisos =
        "camera=(), microphone=(), geolocation=(), payment=(), usb=(), interest-cohort=()";

    public Task InvokeAsync(HttpContext contexto)
    {
        var cabeceras = contexto.Response.Headers;

        cabeceras.ContentSecurityPolicy = politicaDeContenido;
        cabeceras.XContentTypeOptions = "nosniff";
        cabeceras["Referrer-Policy"] = "no-referrer";
        cabeceras["Permissions-Policy"] = PoliticaPermisos;

        return siguiente(contexto);
    }
}
