namespace Facturacion.Server.Modules.Operador.Auth;

/// <summary>
/// La cookie del refresh token del panel de operador. Mismas defensas que la del inquilino:
/// <c>HttpOnly</c>, <c>Secure</c> y <c>SameSite=Strict</c> (ARQUITECTURA.md §4).
///
/// <para><b>Por qué nombre y ruta distintos</b></para>
/// Con el mismo nombre, el navegador mandaría una u otra según cuál se plantó al final, y
/// cerrar sesión en un lado tumbaría el otro. Con la misma ruta, las dos viajarían a los dos
/// sitios. Separando ambas cosas, cada circuito de refresco solo puede ver su propia cookie,
/// que es la garantía de que las dos identidades no se cruzan ni por accidente.
/// </para>
/// </summary>
public static class CookieDeRefreshOperador
{
    public const string Nombre = "refresh_op";

    private const string Ruta = "/api/operador/auth";

    public static void Plantar(HttpResponse respuesta, string valor, TimeSpan duracion)
        => respuesta.Cookies.Append(Nombre, valor, Opciones(DateTimeOffset.UtcNow.Add(duracion)));

    public static void Limpiar(HttpResponse respuesta)
        => respuesta.Cookies.Delete(Nombre, new CookieOptions { Path = Ruta, Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict });

    public static string? Leer(HttpRequest peticion)
        => peticion.Cookies.TryGetValue(Nombre, out var valor) ? valor : null;

    private static CookieOptions Opciones(DateTimeOffset expira) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Ruta,
        Expires = expira,
        IsEssential = true
    };
}
