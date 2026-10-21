using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Shared.Operador;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace Facturacion.Server.Modules.Operador.Auth;

public sealed class DatosDelOperadorInicial
{
    public string Nombre { get; set; } = "";
    public string Correo { get; set; } = "";
    public string Contrasena { get; set; } = "";
    public string Confirmacion { get; set; } = "";
}

public sealed record EstadoDelOperadorInicial(bool HayOperadores, bool HayPrincipal, string? Error = null);

public static class InstalacionDelOperador
{
    public static string? Validar(DatosDelOperadorInicial datos)
    {
        if (string.IsNullOrWhiteSpace(datos.Nombre) || datos.Nombre.Trim().Length > 128)
            return "Introduce un nombre de hasta 128 caracteres.";
        if (string.IsNullOrWhiteSpace(datos.Correo) || datos.Correo.Trim().Length > 254
            || !new EmailAddressAttribute().IsValid(datos.Correo.Trim()))
            return "Introduce un correo válido de hasta 254 caracteres.";
        if (string.IsNullOrWhiteSpace(datos.Contrasena) || datos.Contrasena.Length is < 12 or > 128)
            return "La contraseña debe tener entre 12 y 128 caracteres.";
        if (datos.Contrasena != datos.Confirmacion) return "Las contraseñas no coinciden.";
        return null;
    }

    public static async Task<EstadoDelOperadorInicial> ConsultarAsync(string cadena, CancellationToken ct)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaAcotada(cadena));
            await conexion.OpenAsync(ct);
            await using var comando = conexion.CreateCommand();
            comando.CommandTimeout = 8;
            comando.CommandText = """
                SELECT CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.OperadoresPlataforma) THEN 1 ELSE 0 END AS bit),
                       CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.OperadoresPlataforma WHERE EsPrincipal = 1) THEN 1 ELSE 0 END AS bit);
                """;
            await using var lector = await comando.ExecuteReaderAsync(ct);
            await lector.ReadAsync(ct);
            return new(lector.GetBoolean(0), lector.GetBoolean(1));
        }
        catch (SqlException error)
        {
            return new(false, false, error.Number is 208 or 207
                ? "La base no tiene el esquema de operadores actualizado. Ejecuta el script de instalación antes de continuar."
                : "No se pudo comprobar el operador. Revisa la conexión y los permisos de SQL Server; no se habilitó el alta.");
        }
    }

    public static async Task<bool> CrearAsync(string cadena, DatosDelOperadorInicial datos,
        string traza, string? ip, CancellationToken ct)
    {
        if (Validar(datos) is { } error) throw new ArgumentException(error, nameof(datos));
        var operador = new OperadorPlataforma
        {
            Id = Guid.NewGuid(), Nombre = datos.Nombre.Trim(), Correo = datos.Correo.Trim(),
            CorreoNormalizado = datos.Correo.Trim().ToUpperInvariant(), HashContrasena = "",
            Activo = true, EsPrincipal = true, FechaAltaUtc = DateTime.UtcNow
        };
        operador.HashContrasena = new PasswordHasher<OperadorPlataforma>().HashPassword(operador, datos.Contrasena);
        await using var conexion = new SqlConnection(CadenaAcotada(cadena));
        await conexion.OpenAsync(ct);
        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandTimeout = 15;
        // El bloqueo también protege dos procesos de instalación contra una doble alta.
        comando.CommandText = "SELECT COUNT_BIG(*) FROM dbo.OperadoresPlataforma WITH (TABLOCKX, HOLDLOCK)";
        if (Convert.ToInt64(await comando.ExecuteScalarAsync(ct)) != 0)
        {
            await transaccion.RollbackAsync(ct);
            return false;
        }
        comando.CommandText = """
            INSERT INTO dbo.OperadoresPlataforma
                (Id, Nombre, Correo, CorreoNormalizado, HashContrasena, Activo, EsPrincipal,
                 FechaAltaUtc, AccesosFallidos, BloqueosConsecutivos)
            VALUES (@id, @nombre, @correo, @normalizado, @hash, 1, 1, @momento, 0, 0);
            """;
        comando.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = operador.Id;
        comando.Parameters.Add("@nombre", SqlDbType.NVarChar, 128).Value = operador.Nombre;
        comando.Parameters.Add("@correo", SqlDbType.NVarChar, 254).Value = operador.Correo;
        comando.Parameters.Add("@normalizado", SqlDbType.NVarChar, 254).Value = operador.CorreoNormalizado;
        comando.Parameters.Add("@hash", SqlDbType.NVarChar, 256).Value = operador.HashContrasena;
        comando.Parameters.Add("@momento", SqlDbType.DateTime2).Value = operador.FechaAltaUtc;
        await comando.ExecuteNonQueryAsync(ct);
        comando.Parameters.Clear();
        comando.CommandText = "INSERT INTO dbo.OperadoresPermisos (OperadorId, Permiso) VALUES (@id, @permiso)";
        comando.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = operador.Id;
        var permiso = comando.Parameters.Add("@permiso", SqlDbType.NVarChar, 64);
        foreach (var clave in PermisosDePanel.Todos)
        {
            permiso.Value = clave;
            await comando.ExecuteNonQueryAsync(ct);
        }
        comando.Parameters.Clear();
        comando.CommandText = """
            INSERT INTO dbo.Bitacora
                (OperadorId, Entidad, EntidadId, Accion, ValorNuevo, MomentoUtc, TraceId, IpOrigen)
            VALUES (@id, @entidad, @entidadId, @accion, @retrato, @momento, @traza, @ip);
            """;
        comando.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = operador.Id;
        comando.Parameters.Add("@entidad", SqlDbType.NVarChar, 128).Value = EntidadesDeBitacora.OperadorPlataforma;
        comando.Parameters.Add("@entidadId", SqlDbType.NVarChar, 64).Value = operador.Id.ToString();
        comando.Parameters.Add("@accion", SqlDbType.NVarChar, 64).Value = AccionesDeBitacora.OperadorCreado;
        comando.Parameters.Add("@retrato", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(new
        {
            operador.Nombre, operador.Correo, operador.Activo, operador.EsPrincipal, Origen = "instalacion"
        });
        comando.Parameters.Add("@momento", SqlDbType.DateTime2).Value = operador.FechaAltaUtc;
        comando.Parameters.Add("@traza", SqlDbType.NVarChar, 64).Value = traza;
        comando.Parameters.Add("@ip", SqlDbType.NVarChar, 45).Value = (object?)ip ?? DBNull.Value;
        await comando.ExecuteNonQueryAsync(ct);
        await transaccion.CommitAsync(ct);
        return true;
    }

    private static string CadenaAcotada(string cadena) => new SqlConnectionStringBuilder(cadena)
    {
        ConnectTimeout = 8, ConnectRetryCount = 0
    }.ConnectionString;
}
