using System.Net;
using System.Net.Http.Json;
using System.Text;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Pruebas;

/// <summary>
/// El reintento del manejador clona la petición original y le copia el cuerpo. Un
/// <c>HttpRequestMessage</c> no se puede reenviar tal cual, así que si la copia se hiciera
/// mal el reintento saldría con el cuerpo vacío o truncado, y el fallo sería silencioso: la
/// petición "funciona" —llega una respuesta— pero el servidor recibió otra cosa.
///
/// <para>
/// Esto solo se puede probar con una llamada de verdad al código: un <c>curl</c> contra un
/// endpoint no sirve, porque <see cref="ManejadorDeAutenticacion"/> es un
/// <c>DelegatingHandler</c> de C# que corre dentro del runtime de WebAssembly, en el
/// navegador. <c>curl</c> nunca pasa por ahí.
/// </para>
///
/// <para>
/// El cuerpo de la prueba es de medio megabyte a propósito: mucho más grande que cualquier
/// búfer por omisión, para que un recorte silencioso se note.
/// </para>
/// </summary>
public sealed class ManejadorDeAutenticacionPruebas
{
    /// <summary>Responde en secuencia con las respuestas dadas y guarda cada petición que recibe.</summary>
    private sealed class ManejadorDeSecuencia(params Func<HttpRequestMessage, HttpResponseMessage>[] respuestas)
        : HttpMessageHandler
    {
        private int _indice;

        public List<(HttpRequestMessage Peticion, byte[] Cuerpo)> Recibidas { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage peticion, CancellationToken ct)
        {
            var cuerpo = peticion.Content is null
                ? []
                : await peticion.Content.ReadAsByteArrayAsync(ct);

            Recibidas.Add((peticion, cuerpo));

            return respuestas[Math.Min(_indice++, respuestas.Length - 1)](peticion);
        }
    }

    /// <summary><see cref="IHttpClientFactory"/> de prueba: un manejador fijo por nombre de cliente.</summary>
    private sealed class FabricaDeClientes(Dictionary<string, HttpMessageHandler> manejadores) : IHttpClientFactory
    {
        public HttpClient CreateClient(string nombre) => new(manejadores[nombre])
        {
            BaseAddress = new Uri("https://pruebas.local/")
        };
    }

    private static HttpResponseMessage RespuestaJson(HttpStatusCode estado, object cuerpo) => new(estado)
    {
        Content = JsonContent.Create(cuerpo)
    };

    [Fact]
    public async Task El_reintento_conserva_un_cuerpo_grande_intacto()
    {
        // El cuerpo original: medio megabyte de JSON válido, con contenido no repetitivo
        // para que un desplazamiento de un solo byte también se detecte.
        var elementos = Enumerable.Range(0, 20_000).Select(i => $"concepto-{i}-{Guid.NewGuid()}");
        var cuerpoOriginal = JsonContent.Create(elementos);
        var bytesOriginales = await cuerpoOriginal.ReadAsByteArrayAsync();
        Assert.True(bytesOriginales.Length > 500_000, "El cuerpo de prueba debe ser grande de verdad.");

        // El cliente "auth" (sin el manejador): responde al refresh con una sesión válida.
        var sesionDeRefresco = new RespuestaSesion(
            AccessToken: "token-nuevo-tras-refrescar",
            ExpiraUtc: DateTime.UtcNow.AddMinutes(15),
            Sesion: new SesionDto(
                UsuarioId: Guid.NewGuid(),
                Nombre: "Usuario de prueba",
                Correo: "prueba@ejemplo.mx",
                EmpresaActivaId: Guid.NewGuid(),
                Empresas: [],
                Permisos: [],
                Tema: Temas.Claro));

        var manejadorAuth = new ManejadorDeSecuencia(_ => RespuestaJson(HttpStatusCode.OK, sesionDeRefresco));

        // El cliente "api": la primera petición cae en 401 (token vencido), la segunda —el
        // reintento— es la que hay que revisar con lupa.
        var manejadorApi = new ManejadorDeSecuencia(
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            _ => new HttpResponseMessage(HttpStatusCode.OK));

        var fabrica = new FabricaDeClientes(new Dictionary<string, HttpMessageHandler>
        {
            [ServicioDeSesion.ClienteDesnudo] = manejadorAuth
        });

        var sesion = new ServicioDeSesion(fabrica);

        var manejador = new ManejadorDeAutenticacion(sesion) { InnerHandler = manejadorApi };
        using var cliente = new HttpClient(manejador) { BaseAddress = new Uri("https://pruebas.local/") };

        var respuesta = await cliente.PostAsync("api/conceptos", cuerpoOriginal);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(2, manejadorApi.Recibidas.Count);

        // Esta es la aserción que importa: el segundo envío —el reintento tras el refresco—
        // tiene que traer exactamente los mismos bytes que el original, ni vacío ni truncado.
        var cuerpoDelReintento = manejadorApi.Recibidas[1].Cuerpo;
        Assert.Equal(bytesOriginales, cuerpoDelReintento);

        // El primer envío también debe haber llevado el cuerpo completo: si LoadIntoBufferAsync
        // no se llamara antes del primer intento, este ya saldría corto.
        Assert.Equal(bytesOriginales, manejadorApi.Recibidas[0].Cuerpo);

        // El reintento lleva el token nuevo, no el que provocó el 401.
        Assert.Equal("token-nuevo-tras-refrescar", manejadorApi.Recibidas[1].Peticion.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task Si_el_refresco_falla_no_hay_reintento_y_la_sesion_se_olvida()
    {
        var manejadorAuth = new ManejadorDeSecuencia(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var manejadorApi = new ManejadorDeSecuencia(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var fabrica = new FabricaDeClientes(new Dictionary<string, HttpMessageHandler>
        {
            [ServicioDeSesion.ClienteDesnudo] = manejadorAuth
        });

        var sesion = new ServicioDeSesion(fabrica);
        var manejador = new ManejadorDeAutenticacion(sesion) { InnerHandler = manejadorApi };
        using var cliente = new HttpClient(manejador) { BaseAddress = new Uri("https://pruebas.local/") };

        var respuesta = await cliente.PostAsJsonAsync("api/conceptos", new { dato = "algo" });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        // Un solo intento: sin sesión recuperable no tiene sentido reintentar.
        Assert.Single(manejadorApi.Recibidas);
        Assert.False(sesion.HaySesion);
    }
}
