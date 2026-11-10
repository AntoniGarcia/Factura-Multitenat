using Facturacion.Server.Infra.ActualizacionBase;
using Facturacion.Server.Infra.Seguridad;
using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Facturacion.Pruebas;

public sealed class ActualizacionDeBasePruebas
{
    [Fact]
    public void Acceso_temporal_no_captura_servicios_de_peticion()
    {
        var servicios = new ServiceCollection();
        servicios.AddOptions();
        servicios.AddMemoryCache();
        servicios.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        servicios.AddSingleton<IHostEnvironment>(new Entorno("Development"));
        servicios.AddSingleton<IControlDeIntentos, ControlDeIntentos>();
        servicios.AddSingleton<AccesoTemporalDeBase>();
        using var proveedor = servicios.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Assert.False(proveedor.GetRequiredService<AccesoTemporalDeBase>().Habilitado);
    }

    [Fact]
    public void Go_dentro_de_textos_y_comentarios_no_corta_lotes()
    {
        var lotes = LotesDeSql.Separar("SELECT N'primero\nGO\nsegundo';\n/* GO\nGO\n*/\nGO -- separador\nSELECT 2;");
        Assert.Equal(2, lotes.Count);
        Assert.Contains("primero\nGO\nsegundo", lotes[0].Replace("\r\n", "\n"));
        Assert.Equal("SELECT 2;", lotes[1]);
    }

    [Theory]
    [InlineData("USE otra; SELECT 1;")]
    [InlineData("SELECT 1;\nGO 2")]
    [InlineData(":r archivo.sql")]
    [InlineData("SELECT N'sin cerrar")]
    [InlineData("/* comentario sin cerrar")]
    public void Rechaza_cambio_de_base_comandos_externos_y_texto_incompleto(string sql)
        => Assert.Throws<InvalidDataException>(() => LotesDeSql.Separar(sql));

    [Fact]
    public void Admite_sql_dinamico_con_use_como_texto()
    {
        var lotes = LotesDeSql.Separar("SELECT N'USE no es una instrucción aquí';\nGO\nSELECT [USE], [GO];");
        Assert.Equal(2, lotes.Count);
    }

    [Fact]
    public async Task Pausa_espera_una_operacion_y_rechaza_nuevas()
    {
        var pausa = new PausaDeBase();
        Assert.True(pausa.Entrar());
        var espera = pausa.CerrarAsync();
        Assert.False(espera.IsCompleted);
        Assert.False(pausa.Entrar());
        pausa.Salir();
        await espera.WaitAsync(TimeSpan.FromSeconds(1));
        pausa.Abrir();
        using var acceso = pausa.Tomar();
        Assert.NotNull(acceso);
    }

    [Theory]
    [InlineData("Production", true, 1)]
    [InlineData("Staging", false, 1)]
    [InlineData("Staging", true, -1)]
    public void Acceso_temporal_se_desactiva_en_produccion_o_al_vencer(string entorno, bool habilitado, int dias)
    {
        using var memoria = new MemoryCache(new MemoryCacheOptions());
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MantenimientoBase:Habilitado"] = habilitado.ToString(),
            ["MantenimientoBase:Correo"] = "prueba@example.invalid",
            ["MantenimientoBase:HastaUtc"] = DateTime.UtcNow.AddDays(dias).ToString("O")
        }).Build();
        var acceso = new AccesoTemporalDeBase(configuracion, new Entorno(entorno), new ControlDeIntentos(memoria), Options.Create(new PasswordHasherOptions()));
        Assert.False(acceso.Habilitado);
    }

    private sealed class Entorno(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;
        public string ApplicationName { get; set; } = "Pruebas";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
