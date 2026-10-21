using System.Security.Cryptography;
using System.Text;
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
    public string RutaClave => Path.Combine(_directorio, "clave-instalacion.txt");

    public string? LeerConexion()
    {
        if (!Existe) return null;
        var documento = JsonSerializer.Deserialize<ConexionPersistida>(File.ReadAllText(_archivo))
            ?? throw new InvalidDataException("La configuración de instalación está vacía.");
        if (documento.Version != 1 || string.IsNullOrWhiteSpace(documento.ConexionProtegida))
            throw new InvalidDataException("La configuración de instalación no es compatible.");
        return _protector.Value.Unprotect(documento.ConexionProtegida);
    }

    public void AsegurarClave()
    {
        Directory.CreateDirectory(_directorio);
        try
        {
            using var archivo = new FileStream(RutaClave, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var escritor = new StreamWriter(archivo, Encoding.UTF8);
            escritor.Write(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        }
        catch (IOException) when (File.Exists(RutaClave)) { }
    }

    public bool Autoriza(string clave)
    {
        if (clave.Length is < 32 or > 128) return false;
        var esperada = File.ReadAllText(RutaClave).Trim();
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(clave)),
            SHA256.HashData(Encoding.UTF8.GetBytes(esperada)));
    }

    public async Task<bool> GuardarAsync(string conexion, CancellationToken ct)
    {
        await _escritura.WaitAsync(ct);
        string? temporal = null;
        try
        {
            if (Existe) return false;
            var datos = new ConexionPersistida(1, _protector.Value.Protect(conexion), DateTimeOffset.UtcNow);
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
}
