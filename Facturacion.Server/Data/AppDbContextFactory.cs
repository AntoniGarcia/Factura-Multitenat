using Facturacion.Server.Infra.Tenencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Facturacion.Server.Data;

/// <summary>
/// Construye el contexto para <c>dotnet ef</c>. Sin esto, las herramientas tendrían que
/// levantar el host completo para poder generar una migración.
/// <para>
/// Usa un contexto de tenencia sin empresa: una migración no pertenece a ninguna.
/// </para>
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuracion = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .Configurar(configuracion.GetConnectionString("BaseDeDatos"), ContextoEmpresaFijo.SinEmpresa)
            .Options;

        return new AppDbContext(opciones, ContextoEmpresaFijo.SinEmpresa);
    }
}
