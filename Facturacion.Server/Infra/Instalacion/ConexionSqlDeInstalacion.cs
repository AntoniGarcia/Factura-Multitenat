using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;

namespace Facturacion.Server.Infra.Instalacion;

public sealed class ParametrosDeConexion
{
    [Required, StringLength(256)] public string Servidor { get; set; } = "";
    [Required, StringLength(128)] public string BaseDeDatos { get; set; } = "";
    [StringLength(128)] public string Usuario { get; set; } = "";
    [StringLength(512)] public string Contrasena { get; set; } = "";
    public bool UsarAutenticacionWindows { get; set; }
    public bool ConfiarEnCertificado { get; set; }
}

public sealed record ComprobacionDeConexion(bool Correcta, string Mensaje, int? TablasVisibles = null);

public static class ConexionSqlDeInstalacion
{
    public static bool Validar(ParametrosDeConexion parametros)
        => Validator.TryValidateObject(parametros, new ValidationContext(parametros), [], validateAllProperties: true)
            && !string.IsNullOrWhiteSpace(parametros.Servidor)
            && !string.IsNullOrWhiteSpace(parametros.BaseDeDatos)
            && (parametros.UsarAutenticacionWindows
                ? OperatingSystem.IsWindows()
                : !string.IsNullOrWhiteSpace(parametros.Usuario) && !string.IsNullOrWhiteSpace(parametros.Contrasena));

    public static string CrearCadena(ParametrosDeConexion parametros, bool paraPrueba = false)
        => new SqlConnectionStringBuilder
        {
            DataSource = parametros.Servidor.Trim(),
            InitialCatalog = parametros.BaseDeDatos.Trim(),
            UserID = parametros.UsarAutenticacionWindows ? "" : parametros.Usuario.Trim(),
            Password = parametros.UsarAutenticacionWindows ? "" : parametros.Contrasena,
            IntegratedSecurity = parametros.UsarAutenticacionWindows,
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = parametros.ConfiarEnCertificado,
            PersistSecurityInfo = false,
            Pooling = !paraPrueba,
            ConnectTimeout = paraPrueba ? 8 : 15,
            ConnectRetryCount = 0,
            ApplicationName = paraPrueba ? "Facturacion.Instalacion" : "Facturacion"
        }.ConnectionString;

    public static async Task<ComprobacionDeConexion> ProbarAsync(ParametrosDeConexion parametros, CancellationToken ct)
    {
        try
        {
            await using var conexion = new SqlConnection(CrearCadena(parametros, paraPrueba: true));
            await conexion.OpenAsync(ct);
            await using var comando = conexion.CreateCommand();
            // Solo se consulta metainformación: no se ejecutan migraciones ni se escriben datos.
            comando.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0";
            comando.CommandTimeout = 8;
            var tablas = Convert.ToInt32(await comando.ExecuteScalarAsync(ct));
            return new(true, "Conexión correcta. El servidor pudo abrir la base de datos.", tablas);
        }
        catch (SqlException error)
        {
            var mensaje = error.Number switch
            {
                18456 when parametros.UsarAutenticacionWindows =>
                    "SQL Server rechazó la cuenta de Windows que ejecuta la aplicación. Revisa sus permisos en la base.",
                18456 => "SQL Server rechazó el usuario o la contraseña. Revisa también que admita autenticación SQL.",
                4060 or 916 => "No se pudo abrir esa base. Revisa el nombre y los permisos del usuario SQL.",
                -2 or 53 or 26 or 40 or 10060 or 10061 or 11001 =>
                    "No se pudo contactar SQL Server. Revisa servidor, instancia, puerto y acceso de red desde este equipo.",
                _ => "SQL Server no aceptó la conexión. Revisa los datos, los permisos y el certificado del servidor SQL."
            };
            return new(false, mensaje);
        }
        catch (ArgumentException)
        {
            return new(false, "Los parámetros de conexión no tienen un formato válido.");
        }
    }
}
