using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Server.Modules.Plataforma.Auth;

/// <summary>
/// Los cinco endpoints del circuito de identidad. Cuelgan de <c>/api/</c> como todo el
/// resto: es lo que permite que el service worker excluya la API de la caché con una sola
/// regla y que <c>MapFallbackToFile</c> no se confunda.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/auth").WithTags("Autenticación");

        grupo.MapPost("/iniciar-sesion", IniciarSesion).AllowAnonymous();
        grupo.MapPost("/registro", Registrar).AllowAnonymous();
        grupo.MapPost("/refresh", Refrescar).AllowAnonymous();
        grupo.MapPost("/cerrar-sesion", CerrarSesion).AllowAnonymous();
        grupo.MapPost("/cambiar-empresa", CambiarEmpresa).RequireAuthorization();
        grupo.MapGet("/sesion", ObtenerSesion).RequireAuthorization();
    }

    private static async Task<IResult> Registrar(
        PeticionRegistro peticion, ServicioDeRegistro registro, HttpContext contexto, CancellationToken ct)
    {
        var resultado = await registro.RegistrarAsync(peticion, Ip(contexto), ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(contexto)
            : Results.Ok(resultado.Valor);
    }

    private static async Task<IResult> IniciarSesion(
        PeticionInicioSesion peticion,
        ServicioDeAutenticacion autenticacion,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(peticion.Correo) || string.IsNullOrWhiteSpace(peticion.Contrasena))
            return ErrorNegocio.Validacion("credenciales-incompletas", "Escribe tu correo y tu contraseña.")
                .AResultado(contexto);

        var resultado = await autenticacion.IniciarSesionAsync(peticion, Ip(contexto), Agente(contexto), ct);

        if (resultado.EsFallo)
            return resultado.Error!.AResultado(contexto);

        return Entregar(contexto, resultado.Valor);
    }

    private static async Task<IResult> Refrescar(
        ServicioDeAutenticacion autenticacion,
        HttpContext contexto,
        CancellationToken ct)
    {
        var cookie = CookieDeRefresh.Leer(contexto.Request);

        if (string.IsNullOrEmpty(cookie))
            return ErrorNegocio.Validacion("sesion-no-valida", "Tu sesión terminó. Inicia sesión otra vez.")
                .AResultado(contexto);

        var resultado = await autenticacion.RefrescarAsync(cookie, Ip(contexto), Agente(contexto), ct);

        if (resultado.EsFallo)
        {
            // La cookie ya no sirve para nada: si se deja, cada arranque del Client vuelve a
            // intentar con ella y vuelve a fallar.
            CookieDeRefresh.Limpiar(contexto.Response);
            return resultado.Error!.AResultado(contexto);
        }

        return Entregar(contexto, resultado.Valor);
    }

    private static async Task<IResult> CerrarSesion(
        ServicioDeAutenticacion autenticacion,
        HttpContext contexto,
        CancellationToken ct)
    {
        await autenticacion.CerrarSesionAsync(CookieDeRefresh.Leer(contexto.Request), ct);

        CookieDeRefresh.Limpiar(contexto.Response);

        return Results.NoContent();
    }

    private static async Task<IResult> CambiarEmpresa(
        PeticionCambioEmpresa peticion,
        ServicioDeAutenticacion autenticacion,
        HttpContext contexto,
        CancellationToken ct)
    {
        var resultado = await autenticacion.CambiarEmpresaAsync(peticion.EmpresaId, ct);

        // No toca la cookie: cambiar de empresa emite un access token nuevo, no abre una
        // sesión nueva.
        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> ObtenerSesion(
        ServicioDeAutenticacion autenticacion,
        HttpContext contexto,
        CancellationToken ct)
    {
        var resultado = await autenticacion.ObtenerSesionAsync(ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static IResult Entregar(HttpContext contexto, SesionIniciada sesion)
    {
        CookieDeRefresh.Plantar(contexto.Response, sesion.RefreshEnClaro, sesion.DuracionCookie);

        return Results.Ok(sesion.Respuesta);
    }

    private static string? Ip(HttpContext contexto)
        => contexto.Connection.RemoteIpAddress?.ToString();

    private static string? Agente(HttpContext contexto)
        => contexto.Request.Headers.UserAgent.ToString() is { Length: > 0 } agente ? agente : null;
}
