using System.Security.Claims;
using System.Text;
using System.Reflection;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Instalacion;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Mantenimiento;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;

namespace Facturacion.Server.Infra.ActualizacionBase;

public static class ActualizacionBaseModule
{
    public static void AgregarActualizacionBase(this WebApplicationBuilder constructor)
    {
        var rutas = RutasDeInstalacion.Obtener(constructor);
        var ruta = Path.Combine(rutas.Interna, "appsettings.Mantenimiento.json");
        if (File.Exists(ruta))
        {
            // Este archivo privado solo configura el módulo, nunca reemplaza SQL, JWT o CSD.
            var temporal = new ConfigurationBuilder().AddJsonFile(ruta).Build();
            constructor.Configuration.AddInMemoryCollection(temporal.AsEnumerable()
                .Where(x => x.Key.StartsWith("MantenimientoBase:", StringComparison.OrdinalIgnoreCase)));
        }
        constructor.Services.AddSingleton(rutas);
        constructor.Services.AddSingleton<PausaDeBase>();
        constructor.Services.AddSingleton<AccesoTemporalDeBase>();
        constructor.Services.AddSingleton<ActualizadorDeBase>();
        constructor.Services.AddHostedService(sp => sp.GetRequiredService<ActualizadorDeBase>());
        constructor.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, AutenticacionTemporalDeBase>(AccesoTemporalDeBase.Esquema, _ => { });
        constructor.Services.AddAuthorizationBuilder().AddPolicy(AccesoTemporalDeBase.Esquema, p => p
            .AddAuthenticationSchemes(AccesoTemporalDeBase.Esquema).RequireAuthenticatedUser()
            .RequireClaim("mantenimiento_permiso", "actualizar_base"));
    }

    public static void MapActualizacionBase(this WebApplication app)
    {
        IResult Interfaz(AccesoTemporalDeBase acceso, HttpContext contexto)
        {
            contexto.Response.Headers.CacheControl = "no-store";
            return acceso.Habilitado ? Recurso("modulo.html", "text/html; charset=utf-8") : Results.NotFound();
        }
        app.MapGet("/mantenimiento-base", Interfaz).AllowAnonymous();
        app.MapGet("/api/mantenimiento-base/interfaz", Interfaz).AllowAnonymous();
        app.MapGet("/mantenimiento-base/modulo.js", () => Recurso("modulo.js", "text/javascript; charset=utf-8")).AllowAnonymous();
        app.MapGet("/mantenimiento-base/modulo.css", () => Recurso("modulo.css", "text/css; charset=utf-8")).AllowAnonymous();
        app.MapGet("/api/mantenimiento-base/disponible", async (ClaimsPrincipal principal, AccesoTemporalDeBase acceso, CancellationToken ct) =>
            Results.Ok(Guid.TryParse(principal.FindFirstValue(ClavesDeClaim.Usuario), out var id) && await acceso.DisponibleAsync(id, ct))).RequireAuthorization();
        var grupo = app.MapGroup("/api/mantenimiento-base").WithTags("Mantenimiento temporal")
            .RequireAuthorization(AccesoTemporalDeBase.Esquema);
        grupo.AddEndpointFilter(async (contexto, siguiente) =>
        {
            contexto.HttpContext.Response.Headers.CacheControl = "no-store";
            if (!contexto.HttpContext.Request.IsHttps && contexto.HttpContext.Connection.RemoteIpAddress is { } ip
                && !System.Net.IPAddress.IsLoopback(ip)) return Results.BadRequest();
            try { return await siguiente(contexto); }
            catch (Exception e) when (e is InvalidDataException or DecoderFallbackException)
            {
                return ErrorNegocio.Validacion("actualizacion-no-valida", e is DecoderFallbackException
                    ? "Guarda el script como UTF-8 o UTF-16." : e.Message).AResultado(contexto.HttpContext);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return ErrorNegocio.Validacion("actualizacion-sin-permisos", "No se pudo guardar. Revisa los permisos de modificar en cnf.").AResultado(contexto.HttpContext);
            }
            catch (Microsoft.Data.SqlClient.SqlException)
            {
                return ErrorNegocio.Validacion("actualizacion-sql", "No se pudo consultar la base. Revisa la conexión, el usuario y los permisos SQL.").AResultado(contexto.HttpContext);
            }
        });
        grupo.MapPost("/acceder", async (Credenciales p, HttpContext http, AccesoTemporalDeBase acceso, CancellationToken ct) =>
        {
            if (p.Correo?.Length is not (> 0 and <= 254) || p.Contrasena?.Length is not (> 0 and <= 128)) return Results.Unauthorized();
            var token = await acceso.IniciarAsync(p.Correo, p.Contrasena, http.Connection.RemoteIpAddress?.ToString() ?? "", ct);
            return token is null ? Results.Unauthorized() : Results.Ok(new { token });
        }).AllowAnonymous();
        grupo.MapGet("/", async (ActualizadorDeBase actualizador, CancellationToken ct) => Results.Ok(await actualizador.RevisarBaseAsync(ct)));
        grupo.MapPost("/revisar", async (HttpContext http, ActualizadorDeBase actualizador, CancellationToken ct) =>
        {
            const long limite = ActualizadorDeBase.MaximoArchivo + 65536;
            if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } maximo) maximo.MaxRequestBodySize = limite;
            if (!http.Request.HasFormContentType || http.Request.ContentLength > limite)
                throw new InvalidDataException("Selecciona un script SQL de hasta 2 MB.");
            var form = await http.Request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = limite }, ct);
            var archivo = form.Files.GetFile("archivo");
            if (archivo is null || archivo.Length is 0 or > ActualizadorDeBase.MaximoArchivo
                || !Path.GetExtension(archivo.FileName).Equals(".sql", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Selecciona un script SQL de hasta 2 MB.");
            await using var entrada = archivo.OpenReadStream();
            using var datos = new MemoryStream();
            await entrada.CopyToAsync(datos, ct);
            return Results.Ok(await actualizador.PrepararAsync(datos.ToArray(), Id(http), ct));
        });
        grupo.MapPost("/ejecutar", async (PeticionEjecutarActualizacionBase p, HttpContext http,
            AccesoTemporalDeBase acceso, ActualizadorDeBase actualizador, CancellationToken ct) =>
        {
            if (!p.RespaldoConfirmado) throw new InvalidDataException("Confirma que tienes un respaldo de la base.");
            if (p.Contrasena?.Length is not (> 0 and <= 128) || !await acceso.ConfirmarAsync(Id(http), p.Contrasena, ct))
                return Results.Unauthorized();
            return Results.Accepted("/api/mantenimiento-base", await actualizador.EncolarAsync(p.Id, Id(http), ct));
        });
    }

    private static Guid Id(HttpContext contexto) => Guid.Parse(contexto.User.FindFirstValue(AccesoTemporalDeBase.ClaimUsuario)!);
    private static IResult Recurso(string nombre, string tipo)
    {
        using var entrada = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Facturacion.Server.Infra.ActualizacionBase.Recursos.{nombre}")!;
        using var lector = new StreamReader(entrada);
        return Results.Content(lector.ReadToEnd(), tipo);
    }
    public sealed record Credenciales(string Correo, string Contrasena);
}
