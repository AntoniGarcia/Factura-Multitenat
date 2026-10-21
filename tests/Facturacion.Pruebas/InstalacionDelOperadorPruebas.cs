using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Shared.Operador;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace Facturacion.Pruebas;

public sealed class InstalacionDelOperadorPruebas
{
    [Fact]
    public void Valida_nombre_correo_contrasena_y_confirmacion_en_servidor()
    {
        var datos = Datos();
        Assert.Null(InstalacionDelOperador.Validar(datos));
        datos.Nombre = " ";
        Assert.NotNull(InstalacionDelOperador.Validar(datos));
        datos = Datos();
        datos.Correo = "sin-arroba";
        Assert.NotNull(InstalacionDelOperador.Validar(datos));
        datos = Datos();
        datos.Contrasena = datos.Confirmacion = "corta";
        Assert.NotNull(InstalacionDelOperador.Validar(datos));
        datos = Datos();
        datos.Confirmacion = "otra contraseña";
        Assert.NotNull(InstalacionDelOperador.Validar(datos));
    }

    [SqlInstalacionFact]
    public async Task Alta_concurrente_unica_hash_permisos_bitacora_y_cierre_incluso_inactivo()
    {
        var cadena = Environment.GetEnvironmentVariable("FACTURACION_PRUEBAS_INSTALACION_SQL")!;
        var destino = new SqlConnectionStringBuilder(cadena);
        Assert.StartsWith("FacturacionWizardPruebas_", destino.InitialCatalog);
        var inicial = await InstalacionDelOperador.ConsultarAsync(cadena, default);
        Assert.Null(inicial.Error);
        Assert.False(inicial.HayOperadores);
        var resultados = await Task.WhenAll(
            InstalacionDelOperador.CrearAsync(cadena, Datos(), "prueba-uno", "127.0.0.1", default),
            InstalacionDelOperador.CrearAsync(cadena, Datos(), "prueba-dos", "127.0.0.1", default));
        Assert.Single(resultados, resultado => resultado);
        var posterior = await InstalacionDelOperador.ConsultarAsync(cadena, default);
        Assert.True(posterior.HayOperadores);
        Assert.True(posterior.HayPrincipal);
        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT HashContrasena FROM dbo.OperadoresPlataforma";
        var hash = (string)(await comando.ExecuteScalarAsync())!;
        Assert.NotEqual(Datos().Contrasena, hash);
        Assert.NotEqual(PasswordVerificationResult.Failed,
            new PasswordHasher<OperadorPlataforma>().VerifyHashedPassword(null!, hash, Datos().Contrasena));
        comando.CommandText = "SELECT COUNT(*) FROM dbo.OperadoresPermisos";
        Assert.Equal(PermisosDePanel.Todos.Count, Convert.ToInt32(await comando.ExecuteScalarAsync()));
        comando.CommandText = "SELECT ValorNuevo FROM dbo.Bitacora WHERE Accion = 'operador_creado'";
        var retrato = (string)(await comando.ExecuteScalarAsync())!;
        Assert.DoesNotContain(Datos().Contrasena, retrato);
        Assert.DoesNotContain(hash, retrato);
        comando.CommandText = "UPDATE dbo.OperadoresPlataforma SET Activo = 0, EsPrincipal = 0";
        await comando.ExecuteNonQueryAsync();
        Assert.False(await InstalacionDelOperador.CrearAsync(cadena, Datos(), "prueba-tres", null, default));
        var inactivo = await InstalacionDelOperador.ConsultarAsync(cadena, default);
        Assert.True(inactivo.HayOperadores);
        Assert.False(inactivo.HayPrincipal);
    }

    private static DatosDelOperadorInicial Datos() => new()
    {
        Nombre = "Administrador de prueba", Correo = "instalacion@example.invalid",
        Contrasena = "Contraseña sintética 2026!", Confirmacion = "Contraseña sintética 2026!"
    };
}

public sealed class SqlInstalacionFactAttribute : FactAttribute
{
    public SqlInstalacionFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FACTURACION_PRUEBAS_INSTALACION_SQL")))
            Skip = "Requiere una base temporal con el script instalado y FACTURACION_PRUEBAS_INSTALACION_SQL.";
    }
}
