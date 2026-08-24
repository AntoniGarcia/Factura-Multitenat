using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Facturacion.Server.Infra.Almacen;

/// <summary>
/// Guarda y recupera archivos de empresa <b>cifrados y fuera de <c>wwwroot</c></b>
/// (ARQUITECTURA.md §4): logos, certificados y llaves privadas.
///
/// <para><b>El cifrado va atado a la empresa, no solo a la aplicación</b></para>
/// El protector se deriva de la empresa dueña del archivo, así que un blob de la llantera
/// <b>no se puede descifrar</b> pidiéndolo como la cementera, aunque alguien se equivoque de
/// ruta o consiga construir una. El aislamiento entre empresas deja de depender de que la
/// consulta esté bien escrita y pasa a depender de la criptografía.
/// </summary>
public interface IAlmacenDeArchivos
{
    /// <summary>Cifra el contenido y lo guarda. Devuelve la ruta relativa a registrar en la base.</summary>
    Task<string> GuardarAsync(Guid empresaId, string categoria, byte[] contenido, CancellationToken ct);

    /// <summary>
    /// Descifra y devuelve el contenido. Lanza si la ruta no existe o si el archivo no
    /// pertenece a esa empresa y categoría —lo segundo lo detecta el propio descifrado.
    /// </summary>
    Task<byte[]> LeerAsync(Guid empresaId, string categoria, string rutaRelativa, CancellationToken ct);
}

public sealed class AlmacenDeArchivos(
    IDataProtectionProvider protecciones,
    IOptions<OpcionesDeAlmacen> opciones,
    IHostEnvironment entorno) : IAlmacenDeArchivos
{
    private readonly string _raiz = RutaAbsoluta(opciones.Value.Raiz, entorno);

    public async Task<string> GuardarAsync(Guid empresaId, string categoria, byte[] contenido, CancellationToken ct)
    {
        var rutaRelativa = Path.Combine(empresaId.ToString(), categoria, $"{Guid.NewGuid():N}.bin");
        var rutaAbsoluta = Path.Combine(_raiz, rutaRelativa);

        Directory.CreateDirectory(Path.GetDirectoryName(rutaAbsoluta)!);

        var cifrado = Protector(empresaId, categoria).Protect(contenido);
        await File.WriteAllBytesAsync(rutaAbsoluta, cifrado, ct);

        // Siempre con '/' en la base: la ruta se guarda una vez y se puede leer desde un
        // servidor con otro separador sin tener que migrar la columna.
        return rutaRelativa.Replace(Path.DirectorySeparatorChar, '/');
    }

    public async Task<byte[]> LeerAsync(Guid empresaId, string categoria, string rutaRelativa, CancellationToken ct)
    {
        var rutaAbsoluta = RutaSegura(rutaRelativa);

        if (!File.Exists(rutaAbsoluta))
            throw new FileNotFoundException("El archivo ya no está en el almacén.", rutaRelativa);

        var cifrado = await File.ReadAllBytesAsync(rutaAbsoluta, ct);

        return Protector(empresaId, categoria).Unprotect(cifrado);
    }

    private IDataProtector Protector(Guid empresaId, string categoria)
        => protecciones.CreateProtector("Facturacion.Almacen.v1", categoria, empresaId.ToString());

    /// <summary>
    /// Resuelve la ruta y comprueba que no se salga del almacén. La ruta viene de la base
    /// y no del usuario, pero un <c>..</c> que llegara ahí por cualquier vía no debe poder
    /// leer archivos del sistema.
    /// </summary>
    private string RutaSegura(string rutaRelativa)
    {
        var candidata = Path.GetFullPath(Path.Combine(_raiz, rutaRelativa));

        if (!candidata.StartsWith(_raiz, StringComparison.Ordinal))
            throw new InvalidOperationException("La ruta del archivo apunta fuera del almacén.");

        return candidata;
    }

    private static string RutaAbsoluta(string configurada, IHostEnvironment entorno)
    {
        var raiz = Path.IsPathRooted(configurada)
            ? configurada
            : Path.Combine(entorno.ContentRootPath, configurada);

        // Con separador final: sin él, "…/almacen2" pasaría la comprobación de RutaSegura
        // por empezar igual que "…/almacen".
        return Path.GetFullPath(raiz) + Path.DirectorySeparatorChar;
    }
}

/// <summary>
/// Categorías del almacén. Forman parte del propósito criptográfico, así que un logo no se
/// puede descifrar pidiéndolo como si fuera una llave privada.
/// </summary>
public static class CategoriasDeArchivo
{
    public const string Logo = "logo";
    public const string CertificadoCer = "csd-cer";
    public const string LlavePrivadaKey = "csd-key";

    /// <summary>El CFDI ya timbrado, tal como lo devolvió el PAC. Es el que descarga el usuario.</summary>
    public const string XmlTimbrado = "xml-timbrado";
}
