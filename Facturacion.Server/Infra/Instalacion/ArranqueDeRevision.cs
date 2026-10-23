using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Facturacion.Server.Infra.Instalacion;

public static class ArranqueDeRevision
{
    private const string NombreArchivo = "appsettings.ArranqueRevision.json";

    public static async Task PrepararAlArrancarAsync(WebApplicationBuilder constructor, CancellationToken ct)
    {
        if (!constructor.Configuration.GetValue<bool>("Instalacion:ArranqueAutomatico")) return;
        ExigirStaging(constructor);
        var rutas = RutasDeInstalacion.Obtener(constructor);
        if (constructor.Configuration.GetValue<bool>("Instalacion:JuntoAlServidorRevision"))
        {
            await PrepararAsync(constructor, rutas.Interna, ct);
            return;
        }
        var ruta = constructor.Configuration["Instalacion:Directorio"];
        if (string.IsNullOrWhiteSpace(ruta) || !Path.IsPathRooted(ruta))
            throw new InvalidOperationException("El arranque automático requiere un directorio privado absoluto.");
        ruta = Path.GetFullPath(ruta);
        var sitio = Path.GetFullPath(constructor.Environment.ContentRootPath).TrimEnd(Path.DirectorySeparatorChar);
        if (ruta.Equals(sitio, StringComparison.OrdinalIgnoreCase)
            || ruta.StartsWith(sitio + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La configuración automática debe quedar fuera de la carpeta del sitio.");
        await PrepararAsync(constructor, ruta, ct);
    }

    public static void Cargar(WebApplicationBuilder constructor, string directorio)
    {
        var ruta = Path.Combine(directorio, NombreArchivo);
        if (!File.Exists(ruta)) return;
        ExigirStaging(constructor);
        var protegido = JsonSerializer.Deserialize<ArchivoProtegido>(File.ReadAllText(ruta))
            ?? throw new InvalidDataException("La configuración de revisión está vacía.");
        if (protegido.Version != 1 || string.IsNullOrWhiteSpace(protegido.ContenidoProtegido))
            throw new InvalidDataException("La configuración de revisión no es compatible.");
        var valores = JsonSerializer.Deserialize<Dictionary<string, string?>>(                      //////////
            Protector(directorio).Unprotect(protegido.ContenidoProtegido))
            ?? throw new InvalidDataException("No se pudo leer la configuración de revisión.");
        // Las variables del alojamiento y la configuración explícita conservan prioridad.
        constructor.Configuration.AddInMemoryCollection(valores.Where(par =>
            string.IsNullOrWhiteSpace(constructor.Configuration[par.Key])));
    }

    public static async Task<bool> PrepararAsync(WebApplicationBuilder constructor, string directorio, CancellationToken ct)
    {
        ExigirStaging(constructor);
        var ruta = Path.Combine(directorio, NombreArchivo);
        if (File.Exists(ruta)) return false;
        var datos = Path.Combine(directorio, "datos");
        var llavero = Path.Combine(directorio, "llavero-aplicacion");
        if (Directory.Exists(llavero) && Directory.EnumerateFileSystemEntries(llavero).Any())
            throw new InvalidOperationException("Ya existe un llavero de aplicación. Restaura su configuración original; no se generará otra clave maestra.");
        using var rsa = RSA.Create(3072);
        var solicitud = new CertificateRequest("CN=Facturacion revision - proteccion del llavero",
            rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificado = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(30));
        var valores = new Dictionary<string, string?>
        {
            ["Jwt:Emisor"] = "facturacion-revision",
            ["Jwt:Audiencia"] = "facturacion-client",
            ["Jwt:ClaveDeFirma"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(64)),
            ["Almacen:Raiz"] = datos,
            ["Almacen:RutaLlavero"] = llavero,
            ["Almacen:LlaveMaestraPfx"] = Convert.ToBase64String(certificado.Export(X509ContentType.Pfx)),
            ["Correo:PermitirConfiguracionPosterior"] = "true",
            ["Pac:Modo"] = "Deshabilitado"
        };
        Directory.CreateDirectory(directorio);
        var archivo = new ArchivoProtegido(1, Protector(directorio).Protect(JsonSerializer.Serialize(valores)));
        var temporal = Path.Combine(directorio, $".arranque-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporal, JsonSerializer.Serialize(archivo), ct);
            try { File.Move(temporal, ruta, overwrite: false); }
            catch (IOException) when (File.Exists(ruta)) { return false; }
            Directory.CreateDirectory(datos);
            Directory.CreateDirectory(llavero);
            return true;
        }
        finally { if (File.Exists(temporal)) File.Delete(temporal); }
    }

    private static void ExigirStaging(WebApplicationBuilder constructor)
    {
        if (!constructor.Environment.IsStaging())
            throw new InvalidOperationException("La configuración de revisión solo se permite en Staging. No se utiliza en Production ni para desactivar sus validaciones.");
    }

    private static IDataProtector Protector(string directorio)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("La revisión de esta entrega requiere Windows y DPAPI.");
        return DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(directorio, "llavero-instalacion")), opciones =>
        {
            opciones.SetApplicationName("Facturacion.Instalacion");
            if (OperatingSystem.IsWindows()) opciones.ProtectKeysWithDpapi();
        }).CreateProtector("ArranqueRevision", "v1");
    }

    private sealed record ArchivoProtegido(int Version, string ContenidoProtegido);
}
