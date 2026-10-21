using System.Security.Cryptography.X509Certificates;
using Facturacion.Server.Infra.Instalacion;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace Facturacion.Pruebas;

public sealed class ArranqueDeRevisionPruebas : IDisposable
{
    private readonly string _directorio = Path.Combine(Path.GetTempPath(), "Facturacion-Revision-Pruebas", Guid.NewGuid().ToString("N"));

    [WindowsInstalacionFact]
    public async Task Claves_persistentes_cifradas_sin_pac_ni_correo_ficticio()
    {
        var constructor = Constructor();
        Assert.True(await ArranqueDeRevision.PrepararAsync(constructor, _directorio, default));
        var contenido = File.ReadAllText(Path.Combine(_directorio, "appsettings.ArranqueRevision.json"));
        var estado = InstalacionModule.Preparar(constructor);
        Assert.Empty(estado.ConfiguracionPendiente);
        var firma = constructor.Configuration["Jwt:ClaveDeFirma"]!;
        Assert.Equal(128, firma.Length);
        Assert.DoesNotContain(firma, contenido);
        Assert.Null(constructor.Configuration["Correo:Servidor"]);
        Assert.Null(constructor.Configuration["Pac:Contrasena"]);
        Assert.Equal("Deshabilitado", constructor.Configuration["Pac:Modo"]);
        Assert.True(Path.IsPathRooted(constructor.Configuration["Almacen:Raiz"]));
        using var certificado = X509CertificateLoader.LoadPkcs12(
            Convert.FromBase64String(constructor.Configuration["Almacen:LlaveMaestraPfx"]!), null);
        Assert.True(certificado.HasPrivateKey);
        Assert.False(await ArranqueDeRevision.PrepararAsync(constructor, _directorio, default));
        var otroArranque = Constructor();
        InstalacionModule.Preparar(otroArranque);
        Assert.Equal(firma, otroArranque.Configuration["Jwt:ClaveDeFirma"]);
        Assert.Equal(contenido, File.ReadAllText(Path.Combine(_directorio, "appsettings.ArranqueRevision.json")));
    }

    [WindowsInstalacionFact]
    public async Task La_configuracion_explicita_conserva_prioridad()
    {
        await ArranqueDeRevision.PrepararAsync(Constructor(), _directorio, default);
        var constructor = Constructor();
        var firma = new string('x', 64);
        constructor.Configuration["Jwt:ClaveDeFirma"] = firma;
        ArranqueDeRevision.Cargar(constructor, _directorio);
        Assert.Equal(firma, constructor.Configuration["Jwt:ClaveDeFirma"]);
    }

    [WindowsInstalacionFact]
    public async Task No_se_puede_usar_para_eludir_las_validaciones_de_produccion()
    {
        await ArranqueDeRevision.PrepararAsync(Constructor(), _directorio, default);
        var produccion = Constructor("Production");
        Assert.Throws<InvalidOperationException>(() => ArranqueDeRevision.Cargar(produccion, _directorio));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ArranqueDeRevision.PrepararAsync(produccion, _directorio, default));
    }

    [WindowsInstalacionFact]
    public async Task Arranque_automatico_crea_claves_solo_si_se_habilita_y_las_conserva()
    {
        var constructor = Constructor();
        await ArranqueDeRevision.PrepararAlArrancarAsync(constructor, default);
        Assert.False(Directory.Exists(_directorio));
        constructor.Configuration["Instalacion:ArranqueAutomatico"] = "true";
        await ArranqueDeRevision.PrepararAlArrancarAsync(constructor, default);
        var archivo = Path.Combine(_directorio, "appsettings.ArranqueRevision.json");
        var contenido = File.ReadAllText(archivo);
        await ArranqueDeRevision.PrepararAlArrancarAsync(constructor, default);
        Assert.Equal(contenido, File.ReadAllText(archivo));
        Assert.Empty(InstalacionModule.Preparar(constructor).ConfiguracionPendiente);
    }

    [WindowsInstalacionFact]
    public async Task Arranque_automatico_rechaza_produccion_y_rutas_dentro_del_sitio()
    {
        var constructor = Constructor("Production");
        constructor.Configuration["Instalacion:ArranqueAutomatico"] = "true";
        await Assert.ThrowsAsync<InvalidOperationException>(() => ArranqueDeRevision.PrepararAlArrancarAsync(constructor, default));
        var staging = Constructor();
        staging.Configuration["Instalacion:ArranqueAutomatico"] = "true";
        staging.Configuration["Instalacion:Directorio"] = Path.Combine(staging.Environment.ContentRootPath, "cnf");
        await Assert.ThrowsAsync<InvalidOperationException>(() => ArranqueDeRevision.PrepararAlArrancarAsync(staging, default));
        staging.Configuration["Instalacion:Directorio"] = "cnf";
        await Assert.ThrowsAsync<InvalidOperationException>(() => ArranqueDeRevision.PrepararAlArrancarAsync(staging, default));
        Assert.False(Directory.Exists(_directorio));
    }

    private WebApplicationBuilder Constructor(string entorno = "Staging")
    {
        var constructor = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = entorno, Args = [] });
        constructor.Configuration.Sources.Clear();
        constructor.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Instalacion:Directorio"] = _directorio,
            ["ConnectionStrings:BaseDeDatos"] = "Server=prueba;Database=prueba"
        });
        return constructor;
    }

    public void Dispose()
    {
        if (Directory.Exists(_directorio)) Directory.Delete(_directorio, recursive: true);
    }
}
