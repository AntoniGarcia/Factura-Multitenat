using System.Reflection;
using Facturacion.Server.Data;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Idempotencia;
using Facturacion.Server.Infra.Seguridad;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Contratos;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Facturacion.Server.Infra;

/// <summary>
/// Todo lo transversal: tenencia, base de datos, errores, cabeceras y versión.
/// <c>Program.cs</c> solo llama a estos tres métodos (REPARTO-EQUIPO.md §5).
/// </summary>
public static class InfraestructuraModule
{
    public static IServiceCollection AddInfraestructura(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddHttpContextAccessor();

        // Una sola instancia por petición sirve a las dos interfaces: la de la mitad B
        // (congelada) y la extendida que usa la mitad A.
        servicios.AddScoped<IContextoEmpresaInterno, ContextoEmpresaHttp>();
        servicios.AddScoped<IContextoEmpresa>(sp => sp.GetRequiredService<IContextoEmpresaInterno>());

        // AddDbContext y no AddDbContextPool: el contexto captura la empresa activa al
        // construirse, y un contexto reciclado del pool podría arrastrar la empresa de la
        // petición anterior.
        servicios.AddDbContext<AppDbContext>((sp, opciones) => opciones.Configurar(
            configuracion.GetConnectionString("BaseDeDatos"),
            sp.GetRequiredService<IContextoEmpresaInterno>()));

        servicios.AddHostedService<PurgaDeClavesIdempotencia>();

        servicios.AddOpenApi();

        return servicios;
    }

    public static WebApplication UsePipelineDeInfraestructura(this WebApplication aplicacion)
    {
        // El más externo: nada de lo que venga después puede escapar sin convertirse en
        // Problem Details.
        aplicacion.UseMiddleware<MiddlewareDeExcepciones>();

        // Antes de los archivos estáticos: index.html y el WebAssembly son justamente lo que
        // más necesita la CSP, y UseStaticFiles corta la tubería al responder.
        aplicacion.UseMiddleware<CabecerasDeSeguridad>(
            PoliticaDeContenido.Construir(aplicacion.Environment.WebRootFileProvider));

        if (!aplicacion.Environment.IsDevelopment())
            aplicacion.UseHsts();

        aplicacion.UseHttpsRedirection();
        aplicacion.UseSerilogRequestLogging();

        aplicacion.UseBlazorFrameworkFiles();
        aplicacion.UseStaticFiles();

        aplicacion.UseRouting();

        return aplicacion;
    }

    public static WebApplication MapInfraestructura(this WebApplication aplicacion)
    {
        // La consulta el Client al arrancar: si la versión del servidor no coincide con la
        // suya, fuerza la recarga. Es anónima porque se consulta antes de iniciar sesión.
        aplicacion.MapGet("/api/version", () => Results.Ok(new { version = VersionCompilada() }))
            .AllowAnonymous()
            .WithName("Version");

        if (aplicacion.Environment.IsDevelopment())
            aplicacion.MapOpenApi();

        return aplicacion;
    }

    private static string VersionCompilada()
    {
        var informativa = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrEmpty(informativa))
            return "desconocida";

        // La versión informativa trae el hash del commit después de un '+'.
        var separador = informativa.IndexOf('+');
        return separador < 0 ? informativa : informativa[..separador];
    }
}
