using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Registro;
using Facturacion.Server.Infra.Seguridad;
using Facturacion.Shared.Comun;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Facturacion.Pruebas;

public sealed class SeguridadYVersionFase1Pruebas
{
    [Fact]
    public async Task Las_respuestas_de_error_conservan_las_cabeceras_de_seguridad()
    {
        var middleware = new MiddlewareDeExcepciones(
            _ => throw new ErrorDeNegocioExcepcion(ErrorNegocio.Regla("regla-prueba", "Fallo controlado.")),
            NullLogger<MiddlewareDeExcepciones>.Instance);

        var contexto = new DefaultHttpContext();
        contexto.Request.Path = "/api/emision";
        contexto.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(contexto);

        Assert.Equal(422, contexto.Response.StatusCode);
        Assert.Equal("nosniff", contexto.Response.Headers.XContentTypeOptions.ToString());
        Assert.Equal("no-referrer", contexto.Response.Headers["Referrer-Policy"].ToString());
        Assert.NotEmpty(contexto.Response.Headers["Permissions-Policy"].ToString());
        Assert.Equal(PoliticaDeContenido.Base, contexto.Response.Headers.ContentSecurityPolicy.ToString());
    }

    [Fact]
    public async Task Los_errores_500_conservan_las_cabeceras_de_seguridad()
    {
        var middleware = new MiddlewareDeExcepciones(
            _ => throw new InvalidOperationException("Detalle interno que no debe salir"),
            NullLogger<MiddlewareDeExcepciones>.Instance);

        var contexto = new DefaultHttpContext();
        contexto.Request.Path = "/api/documentos";
        contexto.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(contexto);

        Assert.Equal(500, contexto.Response.StatusCode);
        Assert.Equal("nosniff", contexto.Response.Headers.XContentTypeOptions.ToString());
        Assert.Equal(PoliticaDeContenido.Base, contexto.Response.Headers.ContentSecurityPolicy.ToString());
    }

    [Fact]
    public async Task Las_peticiones_api_usan_csp_base_incluso_despues_de_cachear_el_html()
    {
        const string blazor = "<html><script type=\"importmap\">{\"imports\":{}}</script></html>";
        var middleware = new CabecerasDeSeguridad(async contexto =>
        {
            if (contexto.Request.Path.StartsWithSegments("/api"))
            {
                contexto.Response.ContentType = "application/json";
                await contexto.Response.WriteAsync("{}");
                return;
            }
            contexto.Response.ContentType = "text/html; charset=utf-8";
            await contexto.Response.WriteAsync(blazor);
        });

        var html = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        html.Request.Path = "/";
        await middleware.InvokeAsync(html);
        Assert.Contains("sha256-", html.Response.Headers.ContentSecurityPolicy.ToString());

        var api = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        api.Request.Path = "/api/version";
        await middleware.InvokeAsync(api);
        Assert.Equal(PoliticaDeContenido.Base, api.Response.Headers.ContentSecurityPolicy.ToString());
    }

    [Theory]
    [InlineData("0.1.0+7125d02a1b2c3d4e5f6a", "0.1.0+7125d02a1b2c")]
    [InlineData("0.1.0+abc123", "0.1.0+abc123")]
    [InlineData("0.1.0", "0.1.0")]
    [InlineData("0.1.0+", "0.1.0")]
    [InlineData(null, "desconocida")]
    [InlineData("   ", "desconocida")]
    public void Version_de_compilacion_conserva_el_identificador_de_commit(string? entrada, string esperado)
    {
        Assert.Equal(esperado, VersionDeCompilacion.Formatear(entrada));
    }

    [Fact]
    public void Version_actual_del_ensamblado_compartido_es_determinista_y_no_desconocida()
    {
        Assert.NotEqual("desconocida", VersionDeCompilacion.Actual);
        Assert.StartsWith("0.1.0", VersionDeCompilacion.Actual);
    }

    [Fact]
    public void El_directorio_de_logs_resuelve_carpeta_privada_y_rechaza_wwwroot()
    {
        var constructor = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Staging", Args = [] });
        var rutaBase = Path.Combine(Path.GetTempPath(), "FacturacionLogsPrueba_" + Guid.NewGuid().ToString("N"));
        var cnf = Path.Combine(rutaBase, "cnf");

        var configInstalacion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Instalacion:Directorio"] = cnf })
            .Build();
        Assert.Equal(Path.Combine(rutaBase, "logs"), ConfiguracionSerilog.ResolverDirectorioDeLogs(configInstalacion, constructor.Environment));

        var dentroDeWwwroot = Path.Combine(constructor.Environment.ContentRootPath, "wwwroot", "logs");
        var configPublica = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Registro:Directorio"] = dentroDeWwwroot })
            .Build();
        Assert.Null(ConfiguracionSerilog.ResolverDirectorioDeLogs(configPublica, constructor.Environment));
    }
}
