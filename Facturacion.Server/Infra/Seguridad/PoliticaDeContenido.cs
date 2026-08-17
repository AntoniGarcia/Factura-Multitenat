using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Facturacion.Server.Infra.Seguridad;

/// <summary>
/// La plantilla de la Content-Security-Policy y el cálculo de hashes de script en línea.
///
/// <para><b>Por qué el importmap necesita un hash</b></para>
/// El SDK de Blazor inyecta en <c>index.html</c> un <c>&lt;script type="importmap"&gt;</c>
/// <b>en línea</b> con los nombres huellados del runtime. Con <c>script-src 'self'</c> el
/// navegador lo bloquea, <c>dotnet.js</c> nunca se resuelve y la aplicación se queda para
/// siempre en la pantalla de carga.
///
/// <para>
/// Las salidas eran tres: aflojar la CSP con <c>'unsafe-inline'</c>, apagar el huellado de
/// assets, o autorizar ese script concreto por su hash. CLAUDE.md §4 descarta la primera
/// —<c>wasm-unsafe-eval</c> es lo único que se concede—, y la segunda resultó imposible: ni
/// <c>StaticWebAssetFingerprintingEnabled</c> ni <c>StaticWebAssetsFingerprintContent</c>
/// desactivan el huellado de los archivos del framework. Queda la tercera.
/// </para>
///
/// <para><b>Por qué el hash no se calcula leyendo el archivo del disco</b></para>
/// Se intentó así primero: leer <c>index.html</c> desde <c>IFileProvider</c> al arrancar.
/// Funcionaba en desarrollo por una casualidad —el proveedor compuesto expone ahí una copia
/// ya procesada— pero <b>revienta la aplicación al publicar</b>: en modo publicado no hay
/// ningún <c>IFileProvider</c> que resuelva a un <c>index.html</c> con el importmap ya
/// resuelto, y el arranque lanzaba <c>InvalidOperationException</c> antes de poder escuchar
/// una sola petición. Se descubrió publicando de verdad y corriendo el binario publicado,
/// no leyendo el código: "no des por hecho que funciona porque el proyecto compila"
/// (CLAUDE.md §9).
/// <para>
/// La solución robusta —en <see cref="CabecerasDeSeguridad"/>— calcula el hash de los bytes
/// que de verdad se van a mandar por el cable en la primera respuesta HTML, sin importar
/// qué mecanismo del framework los produjo ni si venían comprimidos.
/// </para>
/// </summary>
public static partial class PoliticaDeContenido
{
    private const string Plantilla =
        "default-src 'self'; " +
        "script-src 'self' 'wasm-unsafe-eval'{0}; " +
        "style-src 'self'; " +

        // La única excepción a «nada de unsafe-*» además de wasm-unsafe-eval, y la más
        // acotada que permite la especificación: MudBlazor escribe atributos style="" en
        // línea —posición de menús flotantes, ancho de barras de progreso— y con solo
        // style-src 'self' el navegador los descarta y los componentes salen mal colocados.
        //
        // Es una directiva aparte y no 'unsafe-inline' dentro de style-src a propósito:
        // style-src-attr cubre nada más el atributo, así que un bloque <style> inyectado
        // sigue bloqueado. Un atributo de estilo no ejecuta script; el riesgo residual
        // sería exfiltrar con url(), y para eso haría falta inyección de HTML, que Razor
        // escapa por omisión.
        //
        // Pendiente: no se comprobó quitándola (docs/REPASO-SEGURIDAD.md).
        "style-src-attr 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "manifest-src 'self'; " +
        "worker-src 'self'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "object-src 'none'";

    /// <summary>Política sin hashes, para todo lo que no es HTML.</summary>
    public static string Base { get; } = string.Format(Plantilla, string.Empty);

    /// <summary>Política que autoriza los scripts en línea que trae ese HTML, y ningún otro.</summary>
    public static string ParaHtml(string html)
    {
        var hashes = ScriptsEnLinea()
            .Matches(html)
            .Select(coincidencia => coincidencia.Groups["contenido"].Value)
            .Where(contenido => !string.IsNullOrWhiteSpace(contenido))
            .Select(Hash)
            .Distinct();

        return string.Format(Plantilla, string.Concat(hashes.Select(hash => $" '{hash}'")));
    }

    private static string Hash(string contenido)
        => "sha256-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(contenido)));

    // Solo los <script> sin src: los que traen src ya quedan cubiertos por 'self'.
    [GeneratedRegex(@"<script(?![^>]*\ssrc=)[^>]*>(?<contenido>[\s\S]*?)</script>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptsEnLinea();
}
