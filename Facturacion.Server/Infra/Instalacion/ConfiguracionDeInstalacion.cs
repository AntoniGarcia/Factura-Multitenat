using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Facturacion.Server.Infra.Instalacion;

public sealed class ConfiguracionDeInstalacion
{
    private readonly SemaphoreSlim _escritura = new(1, 1);
    private readonly string _directorio;
    private readonly string _archivo;
    private readonly Lazy<IDataProtector> _protector;
    private readonly bool _conexionSinCifrar;

    public ConfiguracionDeInstalacion(string directorio, bool conexionSinCifrar = false)
    {
        _conexionSinCifrar = conexionSinCifrar;
        _directorio = Path.GetFullPath(directorio);
        _archivo = Path.Combine(_directorio, "appsettings.Instalacion.json");
        _protector = new Lazy<IDataProtector>(() =>
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("El asistente de esta entrega utiliza protección de Windows.");

            var proveedor = DataProtectionProvider.Create(
                new DirectoryInfo(Path.Combine(_directorio, "llavero-instalacion")), opciones =>
                {
                    opciones.SetApplicationName("Facturacion.Instalacion");
                    if (OperatingSystem.IsWindows()) opciones.ProtectKeysWithDpapi();
                });
            return proveedor.CreateProtector("ConexionSql", "v1");
        });
    }

    public bool Existe => File.Exists(_archivo);
    public string Directorio => _directorio;

    public string? LeerConexion()
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
            if (!_conexionSinCifrar)
                throw new InvalidDataException("La conexión sin cifrar solo se admite en el modo explícito de revisión.");
            var revision = JsonSerializer.Deserialize<ConexionEnClaro>(contenido);
            if (string.IsNullOrWhiteSpace(revision?.Conexion))
                throw new InvalidDataException("La conexión de revisión está vacía.");
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
            object datos = _conexionSinCifrar
                ? new ConexionEnClaro(2, conexion, DateTimeOffset.UtcNow)
                : new ConexionPersistida(1, _protector.Value.Protect(conexion), DateTimeOffset.UtcNow);
            temporal = Path.Combine(_directorio, $".conexion-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(temporal, JsonSerializer.Serialize(datos, new JsonSerializerOptions
            {
                WriteIndented = true
            }), ct);
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

    private sealed record ConexionPersistida(int Version, string ConexionProtegida, DateTimeOffset GuardadaUtc);
    private sealed record ConexionEnClaro(int Version, string Conexion, DateTimeOffset GuardadaUtc);
}
