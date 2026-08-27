using System.Security.Claims;
using Facturacion.Server.Infra.Errores;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Server.Modules.Operador.Auth;

/// <summary>
/// El circuito de identidad del panel de operador. Cuelga de <c>/api/operador/auth</c>, que
/// además de agrupar es la ruta a la que está acotada su cookie.
/// <para>
/// <b>No hay registro público.</b> Los operadores se dan de alta por consola: quien puede
/// crear a quien acredita pagos es el dueño del servidor, no un formulario abierto.
/// </para>
/// </summary>
public static class OperadorAuthEndpoints
{
    public static void MapOperadorAuth(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/operador/auth").WithTags("Operador · Autenticación");

        grupo.MapPost("/iniciar-sesion", IniciarSesion).AllowAnonymous();
        grupo.MapPost("/refresh", Refrescar).AllowAnonymous();
        grupo.MapPost("/cerrar-sesion", CerrarSesion).AllowAnonymous();
        grupo.MapGet("/sesion", ObtenerSesion).RequireAuthorization(PoliticasDeOperador.Operador);

        var perfil = rutas.MapGroup("/api/operador/perfil")
            .WithTags("Operador · Perfil")
            .RequireAuthorization(PoliticasDeOperador.Operador);

        perfil.MapPut("/", ActualizarPerfil);
        perfil.MapPut("/correo", CambiarCorreo);
        perfil.MapPut("/contrasena", CambiarContrasena);
    }

    private static async Task<IResult> ActualizarPerfil(
        PeticionActualizarPerfilOperador peticion,
        ServicioDePerfilDeOperador perfil,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await perfil.ActualizarNombreAsync(operadorId, peticion, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> CambiarCorreo(
        PeticionCambiarCorreoOperador peticion,
        ServicioDePerfilDeOperador perfil,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await perfil.CambiarCorreoAsync(operadorId, peticion, ct);

        return resultado.EsExito
            ? Results.Ok(resultado.Valor)
            : resultado.Error!.AResultado(contexto);
    }

    private static async Task<IResult> CambiarContrasena(
        PeticionCambiarContrasenaOperador peticion,
        ServicioDePerfilDeOperador perfil,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (!TryOperador(contexto, out var operadorId)) return Results.Unauthorized();

        var resultado = await perfil.CambiarContrasenaAsync(operadorId, peticion, ct);

        if (resultado.EsFallo) return resultado.Error!.AResultado(contexto);

        // La cookie ya no vale: el cambio invalidó todas las familias, incluida la de esta
        // sesión. Se limpia aquí para que el panel no reintente con ella.
        CookieDeRefreshOperador.Limpiar(contexto.Response);

        return Results.NoContent();
    }

    private static bool TryOperador(HttpContext contexto, out Guid operadorId)
        => Guid.TryParse(contexto.User.FindFirstValue(ClavesDeClaim.Operador), out operadorId);

    private static async Task<IResult> IniciarSesion(
        PeticionInicioSesionOperador peticion,
        ServicioDeAutenticacionDeOperador autenticacion,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(peticion.Correo) || string.IsNullOrWhiteSpace(peticion.Contrasena))
            return ErrorNegocio.Validacion("credenciales-incompletas", "Escribe tu correo y tu contraseña.")
                .AResultado(contexto);

        var resultado = await autenticacion.IniciarSesionAsync(peticion, Ip(contexto), Agente(contexto), ct);

        return resultado.EsFallo
            ? resultado.Error!.AResultado(contexto)
            : Entregar(contexto, resultado.Valor!);
    }

    private static async Task<IResult> Refrescar(
        ServicioDeAutenticacionDeOperador autenticacion,
        HttpContext contexto,
        CancellationToken ct)
    {
        var cookie = CookieDeRefreshOperador.Leer(contexto.Request);

        if (string.IsNullOrEmpty(cookie))
            return ErrorNegocio.Validacion("sesion-no-valida", "Tu sesión terminó. Inicia sesión otra vez.")
                .AResultado(contexto);

        var resultado = await autenticacion.RefrescarAsync(cookie, Ip(contexto), Agente(contexto), ct);

        if (resultado.EsFallo)
        {
            // La cookie ya no sirve: dejarla haría que cada arranque del panel reintentara con
            // ella y volviera a fallar.
            CookieDeRefreshOperador.Limpiar(contexto.Response);
            return resultado.Error!.AResultado(contexto);
        }

        return Entregar(contexto, resultado.Valor!);
    }

    private static async Task<IResult> CerrarSesion(
        ServicioDeAutenticacionDeOperador autenticacion,
        HttpContext contexto,
        CancellationToken ct)
    {
        await autenticacion.CerrarSesionAsync(CookieDeRefreshOperador.Leer(contexto.Request), ct);

        CookieDeRefreshOperador.Limpiar(contexto.Response);

        return Results.NoContent();
    }

    private static async Task<IResult> ObtenerSesion(
        ServicioDeAutenticacionDeOperador autenticacion,
        HttpContext contexto,
        CancellationToken ct)
    {
        // El identificador sale del claim, nunca de la petición: es la misma regla que impide
        // que el inquilino mande su empresa por parámetro.
        if (!Guid.TryParse(contexto.User.FindFirstValue(ClavesDeClaim.Operador), out var operadorId))
            return Results.Unauthorized();

        var sesion = await autenticacion.ObtenerSesionAsync(operadorId, ct);

        return sesion is null ? Results.Unauthorized() : Results.Ok(sesion);
    }

    private static IResult Entregar(HttpContext contexto, SesionDeOperadorIniciada sesion)
    {
        CookieDeRefreshOperador.Plantar(contexto.Response, sesion.RefreshEnClaro, sesion.DuracionRefresh);

        return Results.Ok(sesion.Respuesta);
    }

    private static string? Ip(HttpContext contexto)
        => contexto.Connection.RemoteIpAddress?.ToString();

    private static string? Agente(HttpContext contexto)
        => contexto.Request.Headers.UserAgent.ToString() is { Length: > 0 } agente ? agente : null;
}
