using Serilog;
using Serilog.Events;

namespace Facturacion.Server.Infra.Registro;

public static class ConfiguracionSerilog
{
    private const long LimiteBytesPorArchivo = 25 * 1024 * 1024;
    private const int ArchivosRetenidos = 30;

    /// <summary>
    /// Deja Serilog como único proveedor de registro, siempre con el enmascarado puesto.
    /// La configuración es en código y no en <c>appsettings</c>: los niveles se pueden
    /// discutir, el enmascarado no.
    /// </summary>
    public static void AgregarRegistro(this WebApplicationBuilder constructor)
    {
        var configuracion = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With<EnriquecedorDatosSensibles>()
            .WriteTo.Enmascarado(destino => destino.Console());

        var directorioLogs = ResolverDirectorioDeLogs(constructor.Configuration, constructor.Environment);
        string? advertenciaArchivo = null;
        if (directorioLogs is not null)
        {
            try
            {
                Directory.CreateDirectory(directorioLogs);
                var rutaArchivo = Path.Combine(directorioLogs, "facturacion-.log");
                configuracion.WriteTo.Enmascarado(destino => destino.File(
                    rutaArchivo,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: ArchivosRetenidos,
                    fileSizeLimitBytes: LimiteBytesPorArchivo,
                    rollOnFileSizeLimit: true,
                    shared: true));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                advertenciaArchivo = ex.Message;
            }
        }

        var registro = configuracion.CreateLogger();
        if (advertenciaArchivo is not null)
            registro.Warning("No se pudo inicializar el archivo de registro en {Directorio}: {Motivo}", directorioLogs, advertenciaArchivo);

        Log.Logger = registro;

        constructor.Logging.ClearProviders();

        // Se registra en los servicios y no solo como proveedor de registro: el
        // UseSerilogRequestLogging del pipeline necesita el DiagnosticContext que agrega
        // esta llamada.
        constructor.Services.AddSerilog(registro, dispose: true);
    }

    /// <summary>
    /// Resuelve la carpeta privada de logs fuera de <c>wwwroot</c>. Devuelve <c>null</c>
    /// si no hay una ruta privada configurada (por ejemplo en desarrollo local).
    /// </summary>
    public static string? ResolverDirectorioDeLogs(IConfiguration configuracion, IHostEnvironment entorno)
    {
        var sitio = Path.GetFullPath(entorno.ContentRootPath);
        var publico = Path.GetFullPath(
                (entorno as IWebHostEnvironment)?.WebRootPath ?? Path.Combine(sitio, "wwwroot"))
            .TrimEnd(Path.DirectorySeparatorChar);

        string? candidato = null;
        var explicito = configuracion["Registro:Directorio"];
        if (!string.IsNullOrWhiteSpace(explicito) && Path.IsPathRooted(explicito))
        {
            candidato = Path.GetFullPath(explicito);
        }
        else if (configuracion.GetValue<bool>("Instalacion:JuntoAlServidorRevision"))
        {
            candidato = Path.GetFullPath(Path.Combine(sitio, "cnf", "logs"));
        }
        else if (configuracion["Instalacion:Directorio"] is { Length: > 0 } dirInstalacion && Path.IsPathRooted(dirInstalacion))
        {
            var normal = Path.GetFullPath(dirInstalacion).TrimEnd(Path.DirectorySeparatorChar);
            candidato = Path.Combine(Directory.GetParent(normal)?.FullName ?? normal, "logs");
        }
        else if (configuracion["Almacen:Raiz"] is { Length: > 0 } almacen && Path.IsPathRooted(almacen))
        {
            var normal = Path.GetFullPath(almacen).TrimEnd(Path.DirectorySeparatorChar);
            candidato = Path.Combine(Directory.GetParent(normal)?.FullName ?? normal, "logs");
        }

        if (candidato is null) return null;

        var normalizado = candidato.TrimEnd(Path.DirectorySeparatorChar);
        if (normalizado.Equals(publico, StringComparison.OrdinalIgnoreCase)
            || normalizado.StartsWith(publico + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return null;

        return normalizado;
    }
}
