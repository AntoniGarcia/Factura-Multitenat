using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Facturacion.Server.Infra.Seguridad;

/// <summary>Autoriza el importmap mediante el hash del HTML enviado. Se calcula sobre la respuesta final porque el archivo publicado puede diferir del disponible al arrancar.</summary>
public static partial class PoliticaDeContenido
{
    private const string Plantilla =
        "default-src 'self'; " +
        "script-src 'self' 'wasm-unsafe-eval'{0}; " +
        "style-src 'self'; " +

        // Permite los atributos style usados por MudBlazor, sin habilitar bloques style en línea.
        // No se ha comprobado el funcionamiento de los componentes sin esta excepción.
        "style-src-attr 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "manifest-src 'self'; " +
        "worker-src 'self'; " +
        // La vista previa de comprobantes carga exclusivamente un Blob creado en memoria
        // con bytes obtenidos por el cliente autenticado. No abre marcos externos.
        "frame-src 'self' blob:; " +
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
