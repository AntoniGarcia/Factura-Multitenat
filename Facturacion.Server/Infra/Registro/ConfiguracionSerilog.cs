using Serilog;
using Serilog.Events;

namespace Facturacion.Server.Infra.Registro;

public static class ConfiguracionSerilog
{
    /// <summary>
    /// Deja Serilog como único proveedor de registro, siempre con el enmascarado puesto.
    /// La configuración es en código y no en <c>appsettings</c>: los niveles se pueden
    /// discutir, el enmascarado no.
    /// </summary>
    public static void AgregarRegistro(this WebApplicationBuilder constructor)
    {
        var registro = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With<EnriquecedorDatosSensibles>()
            .WriteTo.Enmascarado(destino => destino.Console())
            .CreateLogger();

        Log.Logger = registro;

        constructor.Logging.ClearProviders();

        // Se registra en los servicios y no solo como proveedor de registro: el
        // UseSerilogRequestLogging del pipeline necesita el DiagnosticContext que agrega
        // esta llamada.
        constructor.Services.AddSerilog(registro, dispose: true);
    }
}
