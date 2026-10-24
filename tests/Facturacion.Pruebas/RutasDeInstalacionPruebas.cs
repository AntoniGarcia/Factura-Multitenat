using Facturacion.Server.Infra.Instalacion;
using Microsoft.AspNetCore.Builder;

namespace Facturacion.Pruebas;

public sealed class RutasDeInstalacionPruebas
{
    [Fact]
    public async Task Conexion_anterior_en_raiz_se_lee_sin_moverla_ni_borrar_archivos_de_la_raiz()
    {
        var sitio = Path.Combine(Path.GetTempPath(), "Facturacion-Rutas-Pruebas", Guid.NewGuid().ToString("N"));
        try
        {
            var archivo = new ConfiguracionDeInstalacion(sitio, conexionSinCifrar: true);
            const string conexion = "Server=prueba;Database=prueba;Integrated Security=true";
            Assert.True(await archivo.GuardarAsync(conexion, default));
            var obsoleto = Path.Combine(sitio, "clave-instalacion.txt");
            File.WriteAllText(obsoleto, "archivo anterior sin uso");
            var constructor = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Staging", ContentRootPath = sitio, Args = []
            });
            constructor.Configuration["Instalacion:JuntoAlServidorRevision"] = "true";
            constructor.Configuration["Instalacion:ConexionSinCifrarRevision"] = "true";
            var rutas = RutasDeInstalacion.Obtener(constructor);
            Assert.Equal(sitio, rutas.Conexion);
            Assert.Equal(Path.Combine(sitio, "cnf"), rutas.Interna);
            var estado = InstalacionModule.Preparar(constructor);
            Assert.True(estado.ConexionConfigurada);
            Assert.Null(estado.ErrorCarga);
            Assert.Equal(conexion, constructor.Configuration["ConnectionStrings:BaseDeDatos"]);
            Assert.True(File.Exists(obsoleto));
            Assert.False(Directory.Exists(rutas.Interna));
            Assert.False(await estado.Archivo.GuardarAsync("Server=reemplazo", default));
            Assert.Equal(conexion, archivo.LeerConexion());
        }
        finally
        {
            if (Directory.Exists(sitio)) Directory.Delete(sitio, recursive: true);
        }
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", true)]
    [InlineData("Staging", false)]
    public void Modo_junto_al_servidor_exige_revision_y_conexion_legible(string entorno, bool legible)
    {
        var constructor = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = entorno, Args = [] });
        constructor.Configuration["Instalacion:JuntoAlServidorRevision"] = "true";
        constructor.Configuration["Instalacion:ConexionSinCifrarRevision"] = legible.ToString();
        Assert.Throws<InvalidOperationException>(() => RutasDeInstalacion.Obtener(constructor));
    }

    [Fact]
    public void No_mezcla_rutas_externas_con_configuracion_local()
    {
        var constructor = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Staging", Args = [] });
        constructor.Configuration["Instalacion:JuntoAlServidorRevision"] = "true";
        constructor.Configuration["Instalacion:ConexionSinCifrarRevision"] = "true";
        constructor.Configuration["Instalacion:Directorio"] = Path.GetTempPath();
        Assert.Throws<InvalidOperationException>(() => RutasDeInstalacion.Obtener(constructor));
    }
}
