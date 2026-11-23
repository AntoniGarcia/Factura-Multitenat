using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Facturacion.Server.Infra.Instalacion;

public sealed class ConfiguracionDeInstalacion
{
    private readonly SemaphoreSlim _escritura = new(1, 1);
    private readonly string _directorio;
    private readonly string _archivo;
    private readonly Lazy<IDataProtector> _protector;
    public ConfiguracionDeInstalacion(string directorio)
    {
        _directorio = Path.GetFullPath(directorio);
        _archivo = Path.Combine(_directorio, "appsettings.Instalacion.json");
        _protector = new Lazy<IDataProtector>(CrearProtector);
    }

    public bool Existe => File.Exists(_archivo);
    public string Directorio => _directorio;

    public string? LeerConexion(bool migrarFormatoAnterior = false)
    {
        if (!Existe) return null;
        var contenido = File.ReadAllText(_archivo);
        using var formato = JsonDocument.Parse(contenido);
        if (formato.RootElement.ValueKind != JsonValueKind.Object
            || !formato.RootElement.TryGetProperty("Version", out var version)
            || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var numeroVersion))
            throw new InvalidDataException("La configuración de instalación no tiene un formato válido.");
        if (numeroVersion == 2)
        {
            if (!migrarFormatoAnterior)
                throw new InvalidDataException("El formato anterior necesita migrarse desde la instalación de revisión.");
            var revision = JsonSerializer.Deserialize<ConexionEnClaro>(contenido);
            if (string.IsNullOrWhiteSpace(revision?.Conexion))
                throw new InvalidDataException("La conexión de revisión está vacía.");
            MigrarConexion(revision.Conexion, revision.GuardadaUtc);
            return revision.Conexion;
        }
        var documento = JsonSerializer.Deserialize<ConexionPersistida>(contenido)
            ?? throw new InvalidDataException("La configuración de instalación está vacía.");
        if (documento.Version != 1 || string.IsNullOrWhiteSpace(documento.ConexionProtegida))
            throw new InvalidDataException("La configuración de instalación no es compatible.");
        return _protector.Value.Unprotect(documento.ConexionProtegida);
    }

    public async Task<bool> GuardarAsync(string conexion, CancellationToken ct)
    {
        await _escritura.WaitAsync(ct);
        string? temporal = null;
        try
        {
            if (Existe) return false;
            Directory.CreateDirectory(_directorio);
            temporal = Path.Combine(_directorio, $".conexion-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(temporal, Cifrar(conexion, DateTimeOffset.UtcNow), ct);
            try { File.Move(temporal, _archivo, overwrite: false); }
            catch (IOException) when (Existe) { return false; }
            return true;
        }
        finally
        {
            if (temporal is not null && File.Exists(temporal)) File.Delete(temporal);
            _escritura.Release();
        }
    }

    private void MigrarConexion(string conexion, DateTimeOffset guardadaUtc)
    {
        var temporal = Path.Combine(_directorio, $".conexion-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporal, Cifrar(conexion, guardadaUtc));
            // Mantiene el archivo anterior intacto si no se puede escribir el reemplazo cifrado.
            
            File.Replace(temporal, _archivo, destinationBackupFileName: null);
        }
        finally
        {
            if (File.Exists(temporal)) File.Delete(temporal);
        }
    }

    private string Cifrar(string conexion, DateTimeOffset guardadaUtc)
    {
        var protegida = _protector.Value.Protect(conexion);
        // Otro proveedor debe poder leer la clave persistida antes de reemplazar el JSON.
        if (!string.Equals(CrearProtector().Unprotect(protegida), conexion, StringComparison.Ordinal))
            throw new CryptographicException("No se pudo verificar el cifrado de la conexión SQL.");
        return JsonSerializer.Serialize(new ConexionPersistida(1, protegida, guardadaUtc));
    }

    private IDataProtector CrearProtector()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("El asistente de esta entrega utiliza protección de Windows.");

        var proveedor = DataProtectionProvider.Create(
            new DirectoryInfo(Path.Combine(_directorio, "llavero-instalacion")), opciones =>
            {
                opciones.SetApplicationName("Facturacion.Instalacion");
                // LocalMachine evita fallos en IIS cuando LoadUserProfile es false o cambia el AppPool;
                // el acceso al llavero se protege mediante ACL NTFS sobre la carpeta privada.
                if (OperatingSystem.IsWindows()) opciones.ProtectKeysWithDpapi(protectToLocalMachine: true);
            });
        return proveedor.CreateProtector("ConexionSql", "v1");
    }

    private sealed record ConexionPersistida(int Version, string ConexionProtegida, DateTimeOffset GuardadaUtc);
    private sealed record ConexionEnClaro(int Version, string Conexion, DateTimeOffset GuardadaUtc);
}
