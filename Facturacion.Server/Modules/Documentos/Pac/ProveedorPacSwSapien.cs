using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Options;

namespace Facturacion.Server.Modules.Documentos.Pac;

/// <summary>
/// <see cref="IProveedorPac"/> contra SW sapien (SW Smarter Web).
///
/// <para><b>Dos llamadas: autenticar y timbrar</b></para>
/// El token dura dos horas y se renueva solo. Se guarda en memoria del proceso —de ahí que
/// este servicio sea <c>Singleton</c>—: pedir uno nuevo en cada timbrado añadiría un viaje de
/// red al camino con más prisa, y la cuenta tiene tope de autenticaciones.
///
/// <para><b>Por qué consultar es reenviar</b></para>
/// SW no expone «dime qué pasó con esta clave»: la deduplicación va por el encabezado
/// <c>customid</c>, que vive 72 horas. Reenviar el mismo XML con el mismo <c>customid</c>
/// tiene exactamente dos desenlaces, y los dos sirven:
/// <list type="bullet">
///   <item><description>Nunca llegó → lo timbra ahora y devuelve el comprobante completo.</description></item>
///   <item><description>Ya estaba timbrado → contesta <c>CFDI3307</c> con el UUID del timbre original.</description></item>
/// </list>
/// En ninguno de los dos se puede duplicar, que es lo único que no se puede permitir. Por eso
/// <see cref="ConsultarAsync"/> recibe el XML: sin él no hay nada que reenviar, y sin reenviar
/// no hay forma de preguntar.
///
/// <para><b>El UUID sale del XML, no del JSON</b></para>
/// Los campos sueltos de la respuesta cambian entre los distintos servicios de timbrado de SW;
/// el complemento <c>TimbreFiscalDigital</c> dentro del CFDI timbrado no, porque lo fija el
/// SAT. Se lee de ahí y se usa el JSON solo para lo que no viaja en el XML.
/// </summary>
public sealed partial class ProveedorPacSwSapien(
    IHttpClientFactory fabrica,
    IOptions<OpcionesDePac> opciones,
    ILogger<ProveedorPacSwSapien> registro) : IProveedorPac
{
    /// <summary>Nombre del cliente HTTP configurado en <c>DocumentosModule</c>.</summary>
    public const string ClienteHttp = "pac";

    private const string EspacioDeNombresTfd = "http://www.sat.gob.mx/TimbreFiscalDigital";

    /// <summary>Código de SW para «este comprobante ya tiene timbre»: <c>CFDI3307</c>.</summary>
    private const string NumeroDeTimbreDuplicado = "3307";

    /// <summary>
    /// Margen con el que se renueva el token antes de que expire. Sin él, un timbrado que
    /// empieza con el token a punto de vencer se encuentra un 401 a mitad de camino.
    /// </summary>
    private static readonly TimeSpan MargenDeRenovacion = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// El SAT expresa <c>FechaTimbrado</c> en la hora del centro de México, sin desfase en el
    /// texto. Sin esta conversión el comprobante quedaría seis horas corrido en una base que
    /// guarda todo en UTC (CLAUDE.md §5).
    /// </summary>
    private static readonly TimeZoneInfo HusoDelSat = ResolverHusoDelSat();

    private readonly SemaphoreSlim _candadoDeToken = new(1, 1);
    private string? _token;
    private DateTime _tokenExpiraUtc = DateTime.MinValue;

    public Task<RespuestaDePac> TimbrarAsync(string xml, string claveIdempotencia, CancellationToken ct)
        => EnviarAsync(xml, claveIdempotencia, ct);

    /// <inheritdoc cref="ProveedorPacSwSapien"/>
    public Task<RespuestaDePac> ConsultarAsync(string xml, string claveIdempotencia, CancellationToken ct)
        => EnviarAsync(xml, claveIdempotencia, ct);

    // ── Timbrado ────────────────────────────────────────────────────────────────────────

    private async Task<RespuestaDePac> EnviarAsync(string xml, string clave, CancellationToken ct)
    {
        // Un solo reintento, y solo por token vencido. Los reintentos por red los hace
        // ServicioDeTimbrado con espera creciente; duplicarlos aquí multiplicaría la espera
        // real por dos sin que se viera en ningún lado.
        for (var vuelta = 0; vuelta < 2; vuelta++)
        {
            var token = await ObtenerTokenAsync(forzarRenovacion: vuelta > 0, ct);

            if (token is null)
                return new RespuestaDePac(ResultadoDePac.ErrorDeComunicacion, Mensaje: "No se pudo autenticar con el PAC.");

            using var peticion = new HttpRequestMessage(HttpMethod.Post, "/cfdi33/issue/json/v4/b64")
            {
                Content = JsonContent.Create(
                    new { data = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml)) }, options: Json)
            };

            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // La clave de idempotencia del intento es el customid de SW: es lo que impide que
            // un reenvío se convierta en un segundo timbre.
            peticion.Headers.TryAddWithoutValidation("customid", clave);

            using var respuesta = await fabrica.CreateClient(ClienteHttp).SendAsync(peticion, ct);

            if (respuesta.StatusCode == HttpStatusCode.Unauthorized && vuelta == 0)
            {
                registro.LogInformation("El PAC rechazó el token; se renueva y se reintenta una vez.");
                continue;
            }

            return await InterpretarAsync(respuesta, ct);
        }

        return new RespuestaDePac(ResultadoDePac.ErrorDeComunicacion, Mensaje: "El PAC rechazó el token dos veces.");
    }

    private async Task<RespuestaDePac> InterpretarAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

        SobreDeSw? sobre;

        try
        {
            sobre = JsonSerializer.Deserialize<SobreDeSw>(cuerpo, Json);
        }
        catch (JsonException ex)
        {
            // Un cuerpo que no es JSON casi siempre es una página de error de un intermediario,
            // no una respuesta del PAC: no dice nada del comprobante, así que no es un rechazo.
            registro.LogWarning(ex, "El PAC contestó algo que no es JSON con código {Codigo}.", (int)respuesta.StatusCode);

            return new RespuestaDePac(
                ResultadoDePac.ErrorDeComunicacion, Mensaje: $"Respuesta ilegible del PAC ({(int)respuesta.StatusCode}).");
        }

        if (sobre?.Data?.Cfdi is { Length: > 0 } timbrado)
            return DelComprobanteTimbrado(timbrado, sobre.Data.CadenaOriginalSAT);

        var codigo = CodigoDelMensaje(sobre?.Message);
        var detalle = sobre?.MessageDetail ?? sobre?.Message ?? $"El PAC contestó {(int)respuesta.StatusCode}.";

        // Ya tenía timbre. Es la respuesta que resuelve una conciliación: el comprobante está
        // bien timbrado del otro lado y lo único que faltaba era enterarse.
        //
        // Se exige el código y no basta con hallar un UUID en el texto: un rechazo por «el CFDI
        // relacionado no existe» también trae uno —el nuestro—, y darlo por timbrado marcaría
        // como fiscal un comprobante que el PAC rechazó.
        if (EsTimbreDuplicado(codigo) && UuidDelTexto($"{sobre?.Message} {sobre?.MessageDetail}") is { } uuid)
        {
            registro.LogWarning(
                "El PAC reporta timbre previo para esta clave (UUID {Uuid}). Se recupera sin volver a timbrar; " +
                "el XML timbrado no viene en esta respuesta.", uuid);

            return new RespuestaDePac(ResultadoDePac.Timbrado, Uuid: uuid, CodigoError: codigo, Mensaje: detalle);
        }

        // 5xx es del PAC, no del comprobante: reintentarlo puede salir bien, y darlo por
        // rechazado tiraría un comprobante que quizá sí se timbró.
        if ((int)respuesta.StatusCode >= 500)
        {
            registro.LogWarning("El PAC falló con {Codigo}: {Detalle}", (int)respuesta.StatusCode, detalle);
            return new RespuestaDePac(ResultadoDePac.ErrorDeComunicacion, CodigoError: codigo, Mensaje: detalle);
        }

        registro.LogWarning("El PAC rechazó el comprobante: {Codigo} {Detalle}", codigo, detalle);

        return new RespuestaDePac(ResultadoDePac.Rechazado, CodigoError: codigo, Mensaje: detalle);
    }

    /// <summary>
    /// Saca del CFDI timbrado lo que hay que guardar. Si el complemento no está, el documento
    /// no está timbrado por mucho que la respuesta diga que sí, y tratarlo como éxito dejaría
    /// un comprobante sin UUID marcado como timbrado.
    /// </summary>
    private RespuestaDePac DelComprobanteTimbrado(string devuelto, string? cadenaOriginalSat)
    {
        var xmlTimbrado = ComoXml(devuelto);
        XElement? timbre;

        try
        {
            timbre = XDocument.Parse(xmlTimbrado)
                .Descendants(XName.Get("TimbreFiscalDigital", EspacioDeNombresTfd))
                .FirstOrDefault();
        }
        catch (System.Xml.XmlException ex)
        {
            registro.LogError(ex, "El PAC devolvió un CFDI que no se puede leer como XML.");

            return new RespuestaDePac(
                ResultadoDePac.ErrorDeComunicacion, Mensaje: "El PAC devolvió un comprobante ilegible.");
        }

        if (timbre is null)
        {
            registro.LogError("El PAC devolvió un CFDI sin complemento TimbreFiscalDigital.");

            return new RespuestaDePac(
                ResultadoDePac.ErrorDeComunicacion, Mensaje: "El PAC devolvió un comprobante sin timbre.");
        }

        return new RespuestaDePac(
            ResultadoDePac.Timbrado,
            XmlTimbrado: xmlTimbrado,
            Uuid: Guid.TryParse((string?)timbre.Attribute("UUID"), out var uuid) ? uuid : null,
            FechaTimbradoUtc: FechaEnUtc((string?)timbre.Attribute("FechaTimbrado")),
            NoCertificadoSat: (string?)timbre.Attribute("NoCertificadoSAT"),
            SelloSat: (string?)timbre.Attribute("SelloSAT"),
            CadenaOriginalSat: cadenaOriginalSat);
    }

    // ── Autenticación ───────────────────────────────────────────────────────────────────

    private async Task<string?> ObtenerTokenAsync(bool forzarRenovacion, CancellationToken ct)
    {
        if (!forzarRenovacion && _token is { } vigente && DateTime.UtcNow < _tokenExpiraUtc - MargenDeRenovacion)
            return vigente;

        await _candadoDeToken.WaitAsync(ct);

        try
        {
            // Otra petición pudo renovarlo mientras se esperaba el candado: sin esta segunda
            // comprobación, diez timbrados simultáneos piden diez tokens.
            if (!forzarRenovacion && _token is { } recien && DateTime.UtcNow < _tokenExpiraUtc - MargenDeRenovacion)
                return recien;

            var respuesta = await fabrica.CreateClient(ClienteHttp).PostAsJsonAsync(
                "/v2/security/authenticate",
                new { user = opciones.Value.Usuario, password = opciones.Value.Contrasena },
                Json,
                ct);

            var sobre = await respuesta.Content.ReadFromJsonAsync<SobreDeAutenticacion>(Json, ct);

            if (!respuesta.IsSuccessStatusCode || sobre?.Data?.Token is not { Length: > 0 } token)
            {
                // Sin el mensaje del PAC: puede traer de vuelta el usuario, y las credenciales
                // no se registran ni en un error (CLAUDE.md §4).
                registro.LogError(
                    "No se pudo autenticar con el PAC. Código {Codigo}. Revisa las credenciales de 'Pac'.",
                    (int)respuesta.StatusCode);

                _token = null;
                _tokenExpiraUtc = DateTime.MinValue;

                return null;
            }

            _token = token;
            _tokenExpiraUtc = DateTimeOffset.FromUnixTimeSeconds(sobre.Data.ExpiresIn).UtcDateTime;

            return token;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            registro.LogError(ex, "No se pudo alcanzar al PAC para autenticar.");
            return null;
        }
        finally
        {
            _candadoDeToken.Release();
        }
    }

    // ── Apoyo ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El sufijo <c>b64</c> de la ruta describe cómo se <b>manda</b> el comprobante; la
    /// documentación de SW no fija cómo vuelve. Se aceptan las dos formas en vez de apostar
    /// por una: equivocarse fallaría al leer el XML y se reportaría como «respuesta ilegible»,
    /// que manda a buscar el problema justo donde no está.
    /// </summary>
    private static string ComoXml(string devuelto)
    {
        var limpio = devuelto.TrimStart('﻿', ' ', '\r', '\n', '\t');

        if (limpio.StartsWith('<')) return devuelto;

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(limpio));
        }
        catch (FormatException)
        {
            // Ni XML ni base 64: se devuelve tal cual para que el lector de XML dé el error.
            return devuelto;
        }
    }

    /// <summary>Los mensajes de SW llegan como <c>«CFDI3307 - descripción»</c>.</summary>
    private static string? CodigoDelMensaje(string? mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje)) return null;

        var separador = mensaje.IndexOf('-');
        var codigo = (separador > 0 ? mensaje[..separador] : mensaje).Trim();

        return codigo.Length is > 0 and <= 32 ? codigo : null;
    }

    /// <summary>
    /// Por <c>Contains</c> y no por igualdad: SW prefija el código de formas distintas según
    /// el servicio —<c>CFDI3307</c>, <c>307</c>— y lo que identifica al timbre duplicado es
    /// el número, no el prefijo.
    /// </summary>
    private static bool EsTimbreDuplicado(string? codigo)
        => codigo is not null && codigo.Contains(NumeroDeTimbreDuplicado, StringComparison.Ordinal);

    private static Guid? UuidDelTexto(string? texto)
        => texto is not null && RegexDeUuid().Match(texto) is { Success: true } encontrado
            ? Guid.Parse(encontrado.Value)
            : null;

    private static DateTime? FechaEnUtc(string? fechaTimbrado)
    {
        if (string.IsNullOrWhiteSpace(fechaTimbrado)) return null;

        if (!DateTime.TryParse(
                fechaTimbrado, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            return null;

        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), HusoDelSat);
    }

    // UTC si el huso no está instalado: es preferible una fecha corrida y visible a que el
    // timbrado reviente por la zona horaria del servidor, que no es culpa del comprobante.
    private static TimeZoneInfo ResolverHusoDelSat()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex RegexDeUuid();

    // ── Forma de las respuestas de SW ───────────────────────────────────────────────────

    private sealed record SobreDeSw(DatosDeTimbrado? Data, string? Status, string? Message, string? MessageDetail);

    private sealed record DatosDeTimbrado(string? Cfdi, string? CadenaOriginalSAT);

    private sealed record SobreDeAutenticacion(DatosDeAutenticacion? Data, string? Status);

    private sealed record DatosDeAutenticacion(string? Token, long ExpiresIn);
}
