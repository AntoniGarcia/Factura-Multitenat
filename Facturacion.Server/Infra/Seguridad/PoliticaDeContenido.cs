using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;

namespace Facturacion.Server.Infra.Seguridad;

/// <summary>
/// Arma la Content-Security-Policy al arrancar.
///
/// <para><b>Por qué no es una constante</b></para>
/// El SDK de Blazor inyecta en <c>index.html</c> un <c>&lt;script type="importmap"&gt;</c>
/// <b>en línea</b> con los nombres huellados del runtime y sus hashes de integridad. Con
/// <c>script-src 'self'</c> el navegador lo bloquea, <c>dotnet.js</c> nunca se resuelve y la
/// aplicación se queda para siempre en la pantalla de carga.
///
/// <para>
/// Las salidas eran tres: aflojar la CSP con <c>'unsafe-inline'</c>, apagar el huellado de
/// assets, o autorizar ese script concreto por su hash. CLAUDE.md §4 descarta la primera
/// —<c>wasm-unsafe-eval</c> es lo único que se concede—, y la segunda resultó imposible: ni
/// <c>StaticWebAssetFingerprintingEnabled</c> ni <c>StaticWebAssetsFingerprintContent</c>
/// desactivan el huellado de los archivos del framework. Queda la tercera, que además es la
/// forma estándar de servir un SPA estático con CSP estricta.
/// </para>
///
/// <para><b>Por qué recorre varios archivos</b></para>
/// El <c>index.html</c> que se sirve no es el del proyecto: el SDK genera una copia con el
/// importmap ya resuelto. En publicación esa copia es la única que existe, pero en
/// desarrollo el proveedor compuesto del raíz web ofrece las dos, y la primera que devuelve
/// es la del código fuente, con el importmap todavía vacío. Por eso se recogen los hashes de
/// <b>todos</b> los candidatos.
///
/// <para>
/// Eso no afloja nada: un hash autoriza <b>ese contenido exacto</b> y nada más. Sobra el de
/// la variante que no se sirve, y no autoriza ningún script que alguien pudiera inyectar.
/// </para>
/// </summary>
public static partial class PoliticaDeContenido
{
    private const string Plantilla =
        "default-src 'self'; " +
        "script-src 'self' 'wasm-unsafe-eval'{0}; " +
        "style-src 'self'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "manifest-src 'self'; " +
        "worker-src 'self'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "object-src 'none'";

    public static string Construir(IFileProvider archivos)
    {
        var hashes = Candidatos(archivos)
            .SelectMany(HashesDe)
            .Distinct()
            .ToList();

        if (hashes.Count == 0)
            throw new InvalidOperationException(
                "No se encontró ningún index.html con el importmap del runtime. Sin su hash, " +
                "la CSP bloquearía el arranque del WebAssembly en el navegador.");

        return string.Format(Plantilla, string.Concat(hashes.Select(hash => $" '{hash}'")));
    }

    private static IEnumerable<string> Candidatos(IFileProvider archivos)
    {
        var proveedores = archivos is CompositeFileProvider compuesto
            ? compuesto.FileProviders
            : [archivos];

        foreach (var proveedor in proveedores)
        {
            var indice = proveedor.GetFileInfo("index.html");
            if (!indice.Exists) continue;

            using var flujo = indice.CreateReadStream();
            using var lector = new StreamReader(flujo);
            yield return lector.ReadToEnd();
        }
    }

    private static IEnumerable<string> HashesDe(string html)
        => ScriptsEnLinea()
            .Matches(html)
            .Select(coincidencia => coincidencia.Groups["contenido"].Value)
            .Where(contenido => !string.IsNullOrWhiteSpace(contenido))
            .SelectMany(Variantes)
            .Select(Hash);

    // El archivo en disco y el que sale por el cable no siempre coinciden en el fin de
    // línea, y el hash se calcula sobre bytes: un CRLF de más lo cambia por completo. Se
    // autorizan las dos normalizaciones, que siguen siendo contenido exacto y conocido.
    private static IEnumerable<string> Variantes(string contenido)
    {
        var conLf = contenido.Replace("\r\n", "\n");

        yield return conLf;
        yield return conLf.Replace("\n", "\r\n");
    }

    private static string Hash(string contenido)
        => "sha256-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(contenido)));

    // Solo los <script> sin src: los que traen src ya quedan cubiertos por 'self'.
    [GeneratedRegex(@"<script(?![^>]*\ssrc=)[^>]*>(?<contenido>[\s\S]*?)</script>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptsEnLinea();
}
