using Facturacion.Server.Infra.Instalacion;
using Microsoft.AspNetCore.Builder;

namespace Facturacion.Pruebas;

public sealed class RutasDeInstalacionPruebas
{
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
