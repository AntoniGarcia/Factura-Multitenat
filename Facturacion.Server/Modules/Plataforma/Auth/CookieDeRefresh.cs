namespace Facturacion.Server.Modules.Plataforma.Auth;

/// <summary>
/// La cookie del refresh token: <c>HttpOnly</c>, <c>Secure</c> y <c>SameSite=Strict</c>
/// (ARQUITECTURA.md §4). Que el Client y la API compartan origen es lo que permite
/// <c>SameSite=Strict</c> sin fricción.
/// <para>
/// Va acotada a <c>/api/auth</c> a propósito: es la única ruta que la necesita, así que no
/// viaja en cada petición de datos ni en cada descarga del WebAssembly. Menos veces sale al
/// cable, menos oportunidades hay de que se pierda.
/// </para>
/// </summary>
public static class CookieDeRefresh
{
    public const string Nombre = "refresh";

    private const string Ruta = "/api/auth";

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
