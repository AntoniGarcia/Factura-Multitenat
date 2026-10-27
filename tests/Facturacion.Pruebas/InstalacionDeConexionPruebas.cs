using Facturacion.Server.Infra.Instalacion;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Facturacion.Pruebas;

public sealed class InstalacionDeConexionPruebas : IDisposable
{
    private readonly string _directorio = Path.Combine(Path.GetTempPath(), "Facturacion-Instalacion-Pruebas", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Staging", true)]
    [InlineData("Production", false)]
    [InlineData("Development", false)]
    public void Confia_en_certificado_solo_en_revision_y_mantiene_tls_al_probar_y_guardar(string entorno, bool confianza)
    {
        var constructor = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = entorno, Args = [] });
        var parametros = System.Text.Json.JsonSerializer.Deserialize<ParametrosDeConexion>(
            """{"Servidor":"prueba","BaseDeDatos":"prueba","Usuario":"prueba","Contrasena":"prueba","ConfiarEnCertificado":true}""")!;
        foreach (var prueba in new[] { true, false })
        {
            var cadena = new SqlConnectionStringBuilder(ConexionSqlDeInstalacion.CrearCadena(parametros, constructor.Environment, paraPrueba: prueba));
            Assert.Equal(confianza, cadena.TrustServerCertificate);
            Assert.Equal(SqlConnectionEncryptOption.Mandatory, cadena.Encrypt);
        }
    }

    [Fact]
    public void La_cadena_no_interpreta_la_contrasena_como_parametros_de_conexion()
    {
        var parametros = Parametros();
        parametros.Contrasena = "secreto;Integrated Security=true;TrustServerCertificate=true";
        var cadena = new SqlConnectionStringBuilder(ConexionSqlDeInstalacion.CrearCadena(parametros));
        Assert.Equal(parametros.Contrasena, cadena.Password);
        Assert.False(cadena.IntegratedSecurity);
        Assert.False(cadena.TrustServerCertificate);
        Assert.Equal(SqlConnectionEncryptOption.Mandatory, cadena.Encrypt);
        Assert.False(cadena.PersistSecurityInfo);
    }

    [Fact]
    public void No_acepta_datos_vacios_o_desproporcionados()
    {
        Assert.False(ConexionSqlDeInstalacion.Validar(new()));
        var parametros = Parametros();
        parametros.Servidor = new string('a', 257);
        Assert.False(ConexionSqlDeInstalacion.Validar(parametros));
        parametros.Servidor = "  ";
        Assert.False(ConexionSqlDeInstalacion.Validar(parametros));
        Assert.True(ConexionSqlDeInstalacion.Validar(Parametros()));
    }

    [WindowsInstalacionFact]
    public void Windows_no_requiere_credenciales_sql_y_no_las_incluye_si_el_cliente_las_envia()
    {
        var parametros = new ParametrosDeConexion
        {
            Servidor = ".", BaseDeDatos = "FacturacionDev", UsarAutenticacionWindows = true
        };
        Assert.True(ConexionSqlDeInstalacion.Validar(parametros));
        parametros.Usuario = "no-se-debe-usar";
        parametros.Contrasena = "no-se-debe-guardar";
        var cadena = new SqlConnectionStringBuilder(ConexionSqlDeInstalacion.CrearCadena(parametros));
        Assert.True(cadena.IntegratedSecurity);
        Assert.Empty(cadena.UserID);
        Assert.Empty(cadena.Password);
        Assert.Equal(SqlConnectionEncryptOption.Mandatory, cadena.Encrypt);
        Assert.False(cadena.TrustServerCertificate);
    }

    [Fact]
    public void Sql_sigue_exigiendo_usuario_y_contrasena()
    {
        var parametros = Parametros();
        parametros.Contrasena = "";
        Assert.False(ConexionSqlDeInstalacion.Validar(parametros));
        parametros.Contrasena = "clave-prueba";
        parametros.Usuario = "";
        Assert.False(ConexionSqlDeInstalacion.Validar(parametros));
    }

    [WindowsInstalacionFact]
    public async Task El_json_no_contiene_la_contrasena_y_otro_arranque_puede_descifrarlo()
    {
        var archivo = new ConfiguracionDeInstalacion(_directorio);
        var conexion = ConexionSqlDeInstalacion.CrearCadena(Parametros());
        Assert.True(await archivo.GuardarAsync(conexion, default));
        var json = File.ReadAllText(Path.Combine(_directorio, "appsettings.Instalacion.json"));
        Assert.DoesNotContain(Parametros().Contrasena, json);
        Assert.DoesNotContain(conexion, json);
        Assert.Equal(conexion, new ConfiguracionDeInstalacion(_directorio).LeerConexion());
        Assert.Equal(conexion, new ConfiguracionDeInstalacion(_directorio, conexionSinCifrar: true).LeerConexion());
        Assert.False(File.Exists(Path.Combine(_directorio, "clave-instalacion.txt")));
    }

    [WindowsInstalacionFact]
    public async Task Dos_guardados_concurrentes_no_reemplazan_la_instalacion()
    {
        var primero = new ConfiguracionDeInstalacion(_directorio);
        var segundo = new ConfiguracionDeInstalacion(_directorio);
        var resultados = await Task.WhenAll(
            primero.GuardarAsync("Server=primero;Database=prueba", default),
            segundo.GuardarAsync("Server=segundo;Database=prueba", default));
        Assert.Single(resultados, correcto => correcto);
        var conexion = primero.LeerConexion();
        Assert.Contains(conexion, new[] { "Server=primero;Database=prueba", "Server=segundo;Database=prueba" });
        Assert.False(await segundo.GuardarAsync("Server=reemplazo", default));
        Assert.Equal(conexion, primero.LeerConexion());
    }

    [Fact]
    public async Task Revision_explicita_guarda_conexion_legible_sin_crear_llavero()
    {
        var archivo = new ConfiguracionDeInstalacion(_directorio, conexionSinCifrar: true);
        var conexion = ConexionSqlDeInstalacion.CrearCadena(Parametros());
        Assert.True(await archivo.GuardarAsync(conexion, default));
        var json = File.ReadAllText(Path.Combine(_directorio, "appsettings.Instalacion.json"));
        using var documento = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(2, documento.RootElement.GetProperty("Version").GetInt32());
        Assert.Equal(conexion, documento.RootElement.GetProperty("Conexion").GetString());
        Assert.False(Directory.Exists(Path.Combine(_directorio, "llavero-instalacion")));
        Assert.Equal(conexion, new ConfiguracionDeInstalacion(_directorio, conexionSinCifrar: true).LeerConexion());
        Assert.False(await archivo.GuardarAsync("Server=otro", default));
        Assert.Throws<InvalidDataException>(() => new ConfiguracionDeInstalacion(_directorio).LeerConexion());
    }

    [Fact]
    public async Task Revision_carga_la_conexion_legible_sin_reemplazarla()
    {
        var archivo = new ConfiguracionDeInstalacion(_directorio, conexionSinCifrar: true);
        var conexion = ConexionSqlDeInstalacion.CrearCadena(Parametros());
        await archivo.GuardarAsync(conexion, default);
        var constructor = Constructor();
        constructor.Configuration["Instalacion:ConexionSinCifrarRevision"] = "true";
        var estado = InstalacionModule.Preparar(constructor);
        Assert.Null(estado.ErrorCarga);
        Assert.True(estado.ConexionConfigurada);
        Assert.Equal(conexion, constructor.Configuration.GetConnectionString("BaseDeDatos"));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void Rechaza_activar_conexion_sin_cifrar_fuera_de_revision(string entorno)
    {
        var constructor = Constructor();
        constructor.Environment.EnvironmentName = entorno;
        constructor.Configuration["Instalacion:ConexionSinCifrarRevision"] = "true";
        Assert.Throws<InvalidOperationException>(() => InstalacionModule.Preparar(constructor));
    }

    [WindowsInstalacionFact]
    public async Task La_conexion_guardada_se_carga_pero_no_elude_configuracion_pendiente()
    {
        var archivo = new ConfiguracionDeInstalacion(_directorio);
        var conexion = ConexionSqlDeInstalacion.CrearCadena(Parametros());
        await archivo.GuardarAsync(conexion, default);
        var constructor = Constructor();
        var estado = InstalacionModule.Preparar(constructor);
        Assert.True(estado.ConexionConfigurada);
        Assert.True(estado.MostrarAsistente);
        Assert.Null(estado.ErrorCarga);
        Assert.Equal(conexion, constructor.Configuration.GetConnectionString("BaseDeDatos"));
        Assert.Contains("Jwt:ClaveDeFirma", estado.ConfiguracionPendiente);
    }

    [Fact]
    public void Un_archivo_danado_no_reabre_el_formulario_para_reemplazar_la_base()
    {
        Directory.CreateDirectory(_directorio);
        File.WriteAllText(Path.Combine(_directorio, "appsettings.Instalacion.json"), "{}");
        var estado = InstalacionModule.Preparar(Constructor());
        Assert.True(estado.ConexionConfigurada);
        Assert.True(estado.MostrarAsistente);
        Assert.NotNull(estado.ErrorCarga);
    }

    [Fact]
    public void Rechaza_guardar_configuracion_en_wwwroot()
    {
        var constructor = Constructor();
        constructor.Configuration["Instalacion:Directorio"] = Path.Combine(constructor.Environment.ContentRootPath, "wwwroot", "cnf");
        Assert.Throws<InvalidOperationException>(() => InstalacionModule.Preparar(constructor));
    }

    [Fact]
    public void Retira_solo_el_archivo_de_clave_anterior()
    {
        Directory.CreateDirectory(_directorio);
        var rutaAnterior = Path.Combine(_directorio, "clave-instalacion.txt");
        var conservado = Path.Combine(_directorio, "conservar.txt");
        File.WriteAllText(rutaAnterior, "clave de prueba obsoleta");
        File.WriteAllText(conservado, "conservar");
        InstalacionModule.Preparar(Constructor());
        Assert.False(File.Exists(rutaAnterior));
        Assert.Equal("conservar", File.ReadAllText(conservado));
    }

    [Fact]
    public void Una_conexion_preexistente_no_se_reemplaza_ni_se_prueba_durante_el_arranque()
    {
        var constructor = Constructor();
        constructor.Configuration["ConnectionStrings:BaseDeDatos"] = "Server=inaccesible;Database=existente";
        var estado = InstalacionModule.Preparar(constructor);
        Assert.False(estado.MostrarAsistente);
        Assert.True(estado.ConexionConfigurada);
        Assert.False(Directory.Exists(_directorio));
        Assert.Equal("Server=inaccesible;Database=existente", constructor.Configuration.GetConnectionString("BaseDeDatos"));
    }

    private WebApplicationBuilder Constructor()
    {
        var constructor = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Staging", Args = [] });
        constructor.Configuration.Sources.Clear();
        constructor.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Instalacion:Directorio"] = _directorio });
        return constructor;
    }

    private static ParametrosDeConexion Parametros() => new()
    {
        Servidor = "sql-pruebas", BaseDeDatos = "FacturacionPrueba", Usuario = "usuario-pruebas",
        Contrasena = "Clave sintética solo para esta prueba 123!"
    };

    public void Dispose()
    {
        if (Directory.Exists(_directorio)) Directory.Delete(_directorio, recursive: true);
    }
}

public sealed class WindowsInstalacionFactAttribute : FactAttribute
{
    public WindowsInstalacionFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "La protección DPAPI de esta instalación corresponde a Windows.";
    }
}
