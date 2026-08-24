using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;

namespace Facturacion.Server.Infra.Seguridad;

/// <summary>
/// Cabeceras de seguridad en todas las respuestas (ARQUITECTURA.md §4).
///
/// <para><b>Por qué mira el cuerpo del HTML</b></para>
/// La CSP tiene que autorizar por hash el <c>importmap</c> en línea que el SDK de Blazor
/// mete en <c>index.html</c> (ver <see cref="PoliticaDeContenido"/>). Ese HTML se compone en
/// tiempo de compilación por mecanismos del framework que difieren entre desarrollo y
/// publicación —se comprobó publicando de verdad, no se dedujo—, así que en vez de intentar
/// localizar "el" archivo correcto en disco, este middleware calcula el hash de los bytes
/// que de verdad van a salir por el cable en la primera respuesta que resulte ser HTML.
///
/// <para>
/// Se hace una sola vez —la primera navegación— y el resultado se recuerda para el resto de
/// la vida del proceso: el resto de las peticiones no pagan el costo de amortiguar la
/// respuesta.
/// </para>
///
/// <para><b>Por qué también sabe descomprimir</b></para>
/// Con los <c>.br</c>/<c>.gz</c> de la fase de rendimiento ya generados, es posible que el
/// servidor de archivos estáticos decida responder <c>index.html</c> ya comprimido si el
/// cliente lo acepta. Si esta clase hasheara los bytes comprimidos, el hash no coincidiría
/// con lo que el navegador ve después de descomprimir, y la CSP volvería a bloquear el
/// importmap. Por eso, antes de calcular el hash, se deshace la compresión que traiga la
/// respuesta.
/// </para>
/// </summary>
public sealed class CabecerasDeSeguridad(RequestDelegate siguiente)
{
    private const string PoliticaPermisos =
        "camera=(), microphone=(), geolocation=(), payment=(), usb=(), interest-cohort=()";

    // volatile: la primera petición HTML la escribe, todas las demás —de cualquier hilo— la
    // leen. No hace falta más sincronización que eso: calcular la política dos veces por una
    // carrera al arrancar no tiene ningún efecto observable, solo repite trabajo una vez.
    private volatile string? _politicaDelHtml;

    public async Task InvokeAsync(HttpContext contexto)
    {
        Fijas(contexto.Response.Headers);

        var politicaConocida = _politicaDelHtml;

        if (politicaConocida is not null || !PuedeSerHtml(contexto.Request.Path))
        {
            contexto.Response.Headers.ContentSecurityPolicy = politicaConocida ?? PoliticaDeContenido.Base;
            await siguiente(contexto);
            return;
        }

        await PrimeraNavegacion(contexto);
    }

    private static void Fijas(IHeaderDictionary cabeceras)
    {
        cabeceras.XContentTypeOptions = "nosniff";
        cabeceras["Referrer-Policy"] = "no-referrer";
        cabeceras["Permissions-Policy"] = PoliticaPermisos;
    }

    // Las peticiones de assets traen extensión (.js, .wasm, .css, .br...). Las de
    // navegación no, y son las únicas que pueden devolver el HTML del que hay que sacar los
    // hashes; MapFallbackToFile responde a cualquiera de ellas con index.html.
    private static bool PuedeSerHtml(PathString ruta)
    {
        var valor = ruta.Value;

        return string.IsNullOrEmpty(valor)
            || !Path.HasExtension(valor)
            || valor.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
    }

    private async Task PrimeraNavegacion(HttpContext contexto)
    {
        var respuestaReal = contexto.Features.Get<IHttpResponseBodyFeature>()!;

        using var amortiguador = new MemoryStream();

        // Se reemplaza la característica completa y no solo Response.Body: los archivos
        // estáticos se pueden escribir con SendFileAsync, que se saltaría un simple cambio
        // de Response.Body. StreamResponseBodyFeature sabe atender SendFileAsync copiando
        // el archivo al stream que se le da, así que esto captura los dos casos.
        contexto.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(amortiguador));

        try
        {
            await siguiente(contexto);

            if (EsHtml(contexto.Response))
                _politicaDelHtml = PoliticaDeContenido.ParaHtml(TextoPlano(amortiguador, contexto.Response));

            contexto.Response.Headers.ContentSecurityPolicy = _politicaDelHtml ?? PoliticaDeContenido.Base;
        }
        finally
        {
            contexto.Features.Set(respuestaReal);
        }

        amortiguador.Position = 0;
        await amortiguador.CopyToAsync(respuestaReal.Stream, contexto.RequestAborted);
    }

    private static bool EsHtml(HttpResponse respuesta)
        => respuesta.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) is true;

    // Deshace la compresión que el servidor de archivos estáticos haya decidido aplicar
    // —hay .br y .gz de sobra desde que se activó Brotli— para hashear siempre el texto
    // real, nunca los bytes comprimidos.
    //
    // leaveOpen: true en todas partes, a propósito: sin él, disponer el lector o el flujo
    // de descompresión dispone en cascada el MemoryStream que se le pasó, y ese mismo
    // MemoryStream todavía hace falta después, en PrimeraNavegacion, para copiar la
    // respuesta real al cliente. Se aprendió por un ObjectDisposedException en el binario
    // publicado, donde este método sí se ejercita con Content-Encoding real.
    private static string TextoPlano(MemoryStream amortiguador, HttpResponse respuesta)
    {
        amortiguador.Position = 0;

        var codificacion = respuesta.Headers[HeaderNames.ContentEncoding].ToString();

        Stream? envoltura = codificacion switch
        {
            "br" => new BrotliStream(amortiguador, CompressionMode.Decompress, leaveOpen: true),
            "gzip" => new GZipStream(amortiguador, CompressionMode.Decompress, leaveOpen: true),
            _ => null
        };

        try
        {
            using var lector = new StreamReader(envoltura ?? (Stream)amortiguador, Encoding.UTF8, leaveOpen: true);
            return lector.ReadToEnd();
        }
        finally
        {
            envoltura?.Dispose();
        }
    }
}
