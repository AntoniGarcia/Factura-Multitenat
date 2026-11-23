using Facturacion.Server.Infra.Instalacion;
using Microsoft.AspNetCore.Builder;

namespace Facturacion.Pruebas;

public sealed class RutasDeInstalacionPruebas
{
    [WindowsInstalacionFact]
    public async Task Conexion_anterior_en_raiz_se_cifra_en_su_ubicacion_sin_borrar_archivos()
    {
        var sitio = Path.Combine(Path.GetTempPath(), "Facturacion-Rutas-Pruebas", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(sitio);
            var archivo = new ConfiguracionDeInstalacion(sitio);
            const string conexion = "Server=prueba;Database=prueba;Integrated Security=true";
            var rutaConexion = Path.Combine(sitio, "appsettings.Instalacion.json");
            File.WriteAllText(rutaConexion, System.Text.Json.JsonSerializer.Serialize(new
            {
                Version = 2, Conexion = conexion, GuardadaUtc = DateTimeOffset.UtcNow
            }));
            var obsoleto = Path.Combine(sitio, "clave-instalacion.txt");
            File.WriteAllText(obsoleto, "archivo anterior sin uso");
            var constructor = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Staging", ContentRootPath = sitio, Args = []
            });
            constructor.Configuration["Instalacion:JuntoAlServidorRevision"] = "true";
            var rutas = RutasDeInstalacion.Obtener(constructor);
            Assert.Equal(sitio, rutas.Conexion);
            Assert.Equal(Path.Combine(sitio, "cnf"), rutas.Interna);
            var estado = InstalacionModule.Preparar(constructor);
            Assert.True(estado.ConexionConfigurada);
            Assert.Null(estado.ErrorCarga);
            Assert.Equal(conexion, constructor.Configuration["ConnectionStrings:BaseDeDatos"]);
            Assert.True(File.Exists(obsoleto));
            Assert.False(Directory.Exists(rutas.Interna));
            Assert.DoesNotContain(conexion, File.ReadAllText(rutaConexion));
            Assert.False(await estado.Archivo.GuardarAsync("Server=reemplazo", default));
            Assert.Equal(conexion, archivo.LeerConexion());
        }
        finally
        {
            if (Directory.Exists(sitio)) Directory.Delete(sitio, recursive: true);
        }
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void Modo_junto_al_servidor_exige_staging(string entorno)
    {
        var constructor = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = entorno, Args = [] });
        constructor.Configuration["Instalacion:JuntoAlServidorRevision"] = "true";
        Assert.Throws<InvalidOperationException>(() => RutasDeInstalacion.Obtener(constructor));
    }

    [Fact]
    public void No_mezcla_rutas_externas_con_configuracion_local()
    {
        var constructor = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Staging", Args = [] });
        constructor.Configuration["Instalacion:JuntoAlServidorRevision"] = "true";
        constructor.Configuration["Instalacion:Directorio"] = Path.GetTempPath();
        Assert.Throws<InvalidOperationException>(() => RutasDeInstalacion.Obtener(constructor));
    }
}
