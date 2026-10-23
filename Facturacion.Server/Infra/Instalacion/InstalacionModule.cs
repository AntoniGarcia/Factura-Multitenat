using System.Reflection;
using System.Threading.RateLimiting;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Seguridad;
using Facturacion.Server.Modules.Operador.Auth;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.RateLimiting;

namespace Facturacion.Server.Infra.Instalacion;

public sealed record EstadoDeInstalacion(
    ConfiguracionDeInstalacion Archivo, bool MostrarAsistente, bool ConexionConfigurada,
    string? ErrorCarga, string[] ConfiguracionPendiente);

public static class InstalacionModule
{
    public static EstadoDeInstalacion Preparar(WebApplicationBuilder constructor)
    {
        var configuracion = constructor.Configuration;
        var rutas = RutasDeInstalacion.Obtener(constructor);
        var ruta = rutas.Conexion;

        var conexionSinCifrar = configuracion.GetValue<bool>("Instalacion:ConexionSinCifrarRevision");
        if (conexionSinCifrar && !constructor.Environment.IsStaging())
            throw new InvalidOperationException("La conexión sin cifrar solo se permite en Staging para revisión.");
        var archivo = new ConfiguracionDeInstalacion(ruta, conexionSinCifrar);
        ArranqueDeRevision.Cargar(constructor, rutas.Interna);                    /////////////////
        string? errorCarga = null;
        var conexion = configuracion.GetConnectionString("BaseDeDatos");    
        if (string.IsNullOrWhiteSpace(conexion) && archivo.Existe)
        {
            try
            {
                conexion = archivo.LeerConexion();
                configuracion.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:BaseDeDatos"] = conexion
                });
            }
            catch (Exception error) when (error is System.Security.Cryptography.CryptographicException
                or IOException or InvalidDataException or System.Text.Json.JsonException or PlatformNotSupportedException)
            {
                errorCarga = "No se pudo leer la conexión guardada. Conserva cnf y su llavero y utiliza la misma identidad de Windows que realizó la instalación.";
            }
        }

        var pendiente = Pendientes(constructor);
        var forzado = configuracion.GetValue<bool>("Instalacion:SoloConexion");
        var configurada = !string.IsNullOrWhiteSpace(conexion);
        // Una conexión temporalmente caída nunca se interpreta como una instalación nueva.
        var mostrar = forzado || !configurada || (archivo.Existe && pendiente.Length > 0);
        var claveAnterior = Path.Combine(ruta, "clave-instalacion.txt");
        if (File.Exists(claveAnterior)) File.Delete(claveAnterior);
        return new(archivo, mostrar, configurada || archivo.Existe, errorCarga, pendiente);
    }

    public static async Task<bool> EjecutarAsistenteAsync(this WebApplicationBuilder constructor, EstadoDeInstalacion estado)
    {
        constructor.Services.AddRateLimiter(opciones =>
        {
            opciones.AddPolicy("instalacion", contexto => RateLimitPartition.GetFixedWindowLimiter(
                contexto.Connection.RemoteIpAddress?.ToString() ?? "sin-ip", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                }));
            opciones.OnRejected = (rechazo, _) => new ValueTask(ResultadosDeError.EscribirProblema(
                rechazo.HttpContext, 429, "instalacion-limite", "Espera un momento",
                "Se alcanzó el límite de intentos. Vuelve a probar en un minuto."));
        });
        var app = constructor.Build();
        var continuar = 0;
        app.UseMiddleware<MiddlewareDeExcepciones>();
        app.UseMiddleware<CabecerasDeSeguridad>();
        if (!app.Environment.IsDevelopment()) app.UseHsts();
        app.Use(async (contexto, siguiente) =>
        {
            contexto.Response.Headers.CacheControl = "no-store";
            if (contexto.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limite)
                limite.MaxRequestBodySize = 16 * 1024;
            if (!contexto.Request.IsHttps && contexto.Connection.RemoteIpAddress is { } ip
                && !System.Net.IPAddress.IsLoopback(ip))
            {
                await ResultadosDeError.EscribirProblema(contexto, 400, "instalacion-https",
                    "Se necesita HTTPS", "Abre el asistente con HTTPS antes de enviar credenciales.");
                return;
            }
            if (contexto.Request.Path.StartsWithSegments("/api/instalacion")
                && !AccesoAlAsistente.Autoriza(app.Environment, contexto))
            {
                await ResultadosDeError.EscribirProblema(contexto, 403, "instalacion-acceso",
                    "Acceso no permitido", "Abre el asistente desde el servidor o desde el sitio de revisión.");
                return;
            }
            await siguiente(contexto);
        });
        app.UseRateLimiter();
        app.MapGet("/operador", () => Recurso("asistente.html", "text/html; charset=utf-8"));
        app.MapGet("/operador/iniciar-sesion", () => Results.Redirect("/operador"));
        app.MapGet("/instalacion", () => Results.Redirect("/operador"));
        app.MapGet("/instalacion/asistente.css", () => Recurso("asistente.css", "text/css; charset=utf-8"));
        app.MapGet("/instalacion/asistente.js", () => Recurso("asistente.js", "text/javascript; charset=utf-8"));
        app.MapGet("/api/instalacion/estado", async (HttpContext contexto) =>
        {
            var cadena = ConexionActual(constructor.Configuration, estado);
            var operador = cadena is not null
                ? await InstalacionDelOperador.ConsultarAsync(cadena, contexto.RequestAborted) : null;
            return Results.Ok(new
            {
                conexionGuardada = estado.ConexionConfigurada || estado.Archivo.Existe,
                errorCarga = estado.ErrorCarga ?? operador?.Error,
                operadorPendiente = operador is { Error: null, HayOperadores: false },
                operadorCreado = operador?.HayOperadores == true,
                principalExistente = operador?.HayPrincipal == true,
                configuracionPendiente = estado.ConfiguracionPendiente
            });
        }).RequireRateLimiting("instalacion");
        app.MapPost("/api/instalacion/probar", (ParametrosDeConexion parametros, HttpContext contexto) =>
            Procesar(parametros, contexto, estado, constructor, guardar: false)).RequireRateLimiting("instalacion");
        app.MapPost("/api/instalacion/guardar", (ParametrosDeConexion parametros, HttpContext contexto) =>
            Procesar(parametros, contexto, estado, constructor, guardar: true)).RequireRateLimiting("instalacion");
        app.MapPost("/api/instalacion/finalizar", async (HttpContext contexto) =>
        {
            if (!AccesoAlAsistente.Autoriza(constructor.Environment, contexto))
                return Problema(contexto, 403, "instalacion-acceso", "Abre el asistente desde el servidor o desde el sitio de revisión.");
            if (estado.ConfiguracionPendiente.Length > 0 || constructor.Configuration.GetValue<bool>("Instalacion:SoloConexion"))
                return Problema(contexto, 409, "instalacion-pendiente", "La instalación está incompleta.");
            var cadena = ConexionActual(constructor.Configuration, estado);
            if (cadena is null)
                return Problema(contexto, 409, "conexion-pendiente", "Configura la conexión SQL.");
            var operador = await InstalacionDelOperador.ConsultarAsync(cadena, contexto.RequestAborted);
            if (operador.Error is { } error) return Problema(contexto, 400, "operador-comprobacion", error);
            if (!operador.HayPrincipal)
                return Problema(contexto, 409, "operador-pendiente", "Crea el operador principal.");
            if (Interlocked.CompareExchange(ref continuar, 1, 0) == 0)
            {
                // Se cierra el host de instalación después de enviar la respuesta.
                contexto.Response.OnCompleted(() =>
                {
                    app.Lifetime.StopApplication();
                    return Task.CompletedTask;
                });
            }
            return Results.Ok(new { acceso = UrlDeAcceso(app, contexto) });
        }).RequireRateLimiting("instalacion");
        app.MapPost("/api/instalacion/operador", async (DatosDelOperadorInicial datos, HttpContext contexto) =>
        {
            if (!AccesoAlAsistente.Autoriza(constructor.Environment, contexto))
                return Problema(contexto, 403, "instalacion-acceso", "Abre el asistente desde el servidor o desde el sitio de revisión.");
            if (InstalacionDelOperador.Validar(datos) is { } error)
                return Problema(contexto, 400, "operador-datos", error);
            var cadena = ConexionActual(constructor.Configuration, estado);
            if (cadena is null)
                return Problema(contexto, 409, "conexion-pendiente", "Primero guarda una conexión válida.");
            var comprobacion = await InstalacionDelOperador.ConsultarAsync(cadena, contexto.RequestAborted);
            if (comprobacion.Error is { } fallo)
                return Problema(contexto, 400, "operador-comprobacion", fallo);
            if (comprobacion.HayOperadores)
                return Problema(contexto, 409, "operador-existente", "El operador ya está registrado.");
            try
            {
                var creado = await InstalacionDelOperador.CrearAsync(cadena, datos,
                    System.Diagnostics.Activity.Current?.Id ?? contexto.TraceIdentifier,
                    contexto.Connection.RemoteIpAddress?.ToString(), contexto.RequestAborted);
                return creado ? Results.Ok(new { mensaje = "Operador creado." })
                    : Problema(contexto, 409, "operador-existente", "El operador ya está registrado.");
            }
            catch (SqlException)
            {
                return Problema(contexto, 400, "operador-guardar", "No se pudo guardar el operador. Comprueba el esquema y los permisos de escritura; el alta no se completó.");
            }
        }).RequireRateLimiting("instalacion");
        app.MapGet("/", () => Results.Redirect("/operador"));
        app.MapFallback(async contexto => await ResultadosDeError.EscribirProblema(
            contexto, 404, "instalacion-ruta", "Ruta no disponible",
            "Durante la instalación solo están disponibles los asistentes de conexión y operador."));
        app.Logger.LogInformation("Instalación inicial disponible en /operador.");
        await app.RunAsync();
        return Volatile.Read(ref continuar) == 1;
    }

    private static string UrlDeAcceso(WebApplication app, HttpContext contexto)
    {
        if (!contexto.Request.IsHttps && contexto.Connection.RemoteIpAddress is { } ip
            && System.Net.IPAddress.IsLoopback(ip))
        {
            var httpsLocal = app.Urls.FirstOrDefault(url => Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps && uri.IsLoopback);
            if (httpsLocal is not null) return httpsLocal.TrimEnd('/') + "/operador/iniciar-sesion";
        }
        return "/operador/iniciar-sesion";
    }

    public static async Task<EstadoDeInstalacion> ComprobarOperadorAsync(
        WebApplicationBuilder constructor, EstadoDeInstalacion estado)
    {
        if (estado.MostrarAsistente) return estado;
        var cadena = ConexionActual(constructor.Configuration, estado);
        if (cadena is null) return estado;
        var operador = await InstalacionDelOperador.ConsultarAsync(cadena, CancellationToken.None);
        if (operador.HayOperadores && operador.Error is null) return estado;
        return estado with { MostrarAsistente = true, ErrorCarga = operador.Error };
    }

    private static string? ConexionActual(IConfiguration configuracion, EstadoDeInstalacion estado)
    {
        if (estado.ErrorCarga is not null) return null;
        var cadena = configuracion.GetConnectionString("BaseDeDatos");
        return !string.IsNullOrWhiteSpace(cadena) ? cadena : estado.Archivo.LeerConexion();
    }

    private static async Task<IResult> Procesar(
        ParametrosDeConexion parametros, HttpContext contexto, EstadoDeInstalacion estado, WebApplicationBuilder constructor, bool guardar)
    {
        if (!AccesoAlAsistente.Autoriza(constructor.Environment, contexto))
            return Problema(contexto, 403, "instalacion-acceso", "Abre el asistente desde el servidor o desde el sitio de revisión.");
        if (estado.ConexionConfigurada || estado.Archivo.Existe)
            return Problema(contexto, 409, "conexion-configurada", "La conexión ya está guardada. Este asistente no permite reemplazarla.");
        if (!ConexionSqlDeInstalacion.Validar(parametros))
            return Problema(contexto, 400, "parametros-conexion", "Completa servidor y base. Con autenticación SQL también se requieren usuario y contraseña; la autenticación de Windows requiere ejecutar la aplicación en Windows.");

        var prueba = await ConexionSqlDeInstalacion.ProbarAsync(parametros, contexto.RequestAborted);
        if (!prueba.Correcta) return Problema(contexto, 400, "conexion-sql", prueba.Mensaje);
        if (!guardar) return Results.Ok(prueba);

        // Se vuelve a probar al guardar: no se confía en el resultado declarado por el navegador.
        if (!await estado.Archivo.GuardarAsync(ConexionSqlDeInstalacion.CrearCadena(parametros), contexto.RequestAborted))
            return Problema(contexto, 409, "conexion-configurada", "Otra solicitud ya guardó la conexión.");
        return Results.Ok(new { mensaje = "Conexión guardada.", prueba.TablasVisibles });
    }

    private static IResult Problema(HttpContext contexto, int estado, string codigo, string detalle)
        => Results.Json(new Facturacion.Shared.Comun.DetalleProblema(
            Tipo: $"urn:facturacion:error:{codigo}", Titulo: "Revisa la instalación", Estado: estado,
            Detalle: detalle, Instancia: contexto.Request.Path,
            TraceId: System.Diagnostics.Activity.Current?.Id ?? contexto.TraceIdentifier, Errores: null),
            statusCode: estado, contentType: "application/problem+json");

    internal static IResult Recurso(string nombre, string tipo)
    {
        using var entrada = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            $"Facturacion.Server.Infra.Instalacion.Recursos.{nombre}")
            ?? throw new InvalidOperationException("No se encontró un recurso del asistente.");
        using var lector = new StreamReader(entrada);
        return Results.Content(lector.ReadToEnd(), tipo);
    }

    private static string[] Pendientes(WebApplicationBuilder constructor)
    {
        var c = constructor.Configuration;
        var pendientes = new List<string>();
        foreach (var clave in new[] { "Jwt:Emisor", "Jwt:Audiencia", "Almacen:LlaveMaestraPfx" })
            if (string.IsNullOrWhiteSpace(c[clave])) pendientes.Add(clave);
        if ((c["Jwt:ClaveDeFirma"]?.Length ?? 0) < 32) pendientes.Add("Jwt:ClaveDeFirma");
        if (!constructor.Environment.IsDevelopment())
        {
            foreach (var clave in new[] { "Almacen:Raiz", "Almacen:RutaLlavero" })
                if (!Path.IsPathRooted(c[clave])) pendientes.Add(clave);
            if (string.IsNullOrWhiteSpace(c["Correo:Servidor"])
                && !(constructor.Environment.IsStaging() && c.GetValue<bool>("Correo:PermitirConfiguracionPosterior")))
                pendientes.Add("Correo:Servidor");
            var pac = c["Pac:Modo"] ?? "Real";
            if (pac.Equals("Real", StringComparison.OrdinalIgnoreCase))
                foreach (var clave in new[] { "Pac:UrlBase", "Pac:Usuario", "Pac:Contrasena" })
                    if (string.IsNullOrWhiteSpace(c[clave])) pendientes.Add(clave);
            else if (!pac.Equals("Deshabilitado", StringComparison.OrdinalIgnoreCase)
                || constructor.Environment.IsProduction()) pendientes.Add("Pac:Modo (usar Staging para revisión sin PAC)");
        }
        return [.. pendientes];
    }
}
