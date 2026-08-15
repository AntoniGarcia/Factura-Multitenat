using System.Text;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Idempotencia;
using Facturacion.Server.Infra.Tenencia;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Facturacion.Pruebas;

/// <summary>
/// La tercera prueba obligatoria de la fase 7: dos POST de compra con la misma
/// <c>Idempotency-Key</c> producen una sola compra y la misma respuesta.
///
/// <para><b>Se prueba el filtro, no un doble del filtro</b></para>
/// Corre <see cref="FiltroDeIdempotencia"/> de verdad contra SQL Server de verdad, porque lo
/// que sostiene la garantía es el índice único de la base: es él quien decide cuál de dos
/// peticiones simultáneas gana. Un doble en memoria probaría la parte fácil —el camino sin
/// concurrencia— y se saltaría exactamente lo que puede fallar.
/// </summary>
public sealed class IdempotenciaDeCompraPruebas : IAsyncLifetime
{
    private const string Conexion =
        "Server=.;Database=FacturacionPruebasIdempotencia;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    private const string Ruta = "/api/timbres/comprar";

    private readonly Guid _cuenta = Guid.NewGuid();
    private readonly Guid _empresa = Guid.NewGuid();
    private readonly Guid _usuario = Guid.NewGuid();

    /// <summary>
    /// Lo mínimo que <c>IResult.ExecuteAsync</c> necesita del contenedor para escribir la
    /// respuesta: opciones de JSON y registro. Fuera de una aplicación real no hay nadie que
    /// se lo dé.
    /// </summary>
    private static readonly IServiceProvider Servicios = new ServiceCollection()
        .AddLogging()
        .AddOptions()
        .BuildServiceProvider();

    private static AppDbContext Contexto(Guid? empresa)
    {
        var tenencia = new ContextoEmpresaFijo(empresa);
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .Configurar(Conexion, tenencia)
            .Options;

        return new AppDbContext(opciones, tenencia);
    }

    public async Task InitializeAsync()
    {
        await using var db = Contexto(null);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        db.Cuentas.Add(new Cuenta
        {
            Id = _cuenta,
            Nombre = "Cuenta de prueba",
            CorreoContacto = "pruebas@ejemplo.mx",
            FechaAltaUtc = DateTime.UtcNow
        });

        db.Empresas.Add(new Empresa
        {
            Id = _empresa,
            CuentaId = _cuenta,
            Rfc = "CBA010101BB2",
            NombreFiscal = "EMPRESA DE PRUEBA",
            RegimenFiscal = "601",
            CodigoPostalExpedicion = "42000",
            ZonaHoraria = "Central Standard Time (Mexico)",
            FechaAltaUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = Contexto(null);
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Dos_peticiones_con_la_misma_clave_producen_una_sola_compra_y_la_misma_respuesta()
    {
        const string clave = "compra-de-la-llantera-2026-08-15";
        const string cuerpo = """{"paqueteId":"9c1f0a10-0000-4000-8000-000000000001"}""";

        var ejecuciones = 0;

        // Simula el endpoint: devuelve una compra nueva cada vez que de verdad se ejecuta. Que
        // el identificador cambie en cada ejecución es a propósito: si el filtro dejara pasar
        // la segunda petición, las dos respuestas serían distintas y la prueba lo vería.
        Task<object?> Endpoint(EndpointFilterInvocationContext _)
        {
            ejecuciones++;
            return Task.FromResult<object?>(
                Results.Ok(new { compraId = Guid.NewGuid(), timbres = 500 }));
        }

        var primera = await Invocar(clave, cuerpo, Endpoint);
        var segunda = await Invocar(clave, cuerpo, Endpoint);

        // El endpoint corrió una sola vez: la segunda petición no llegó a él.
        Assert.Equal(1, ejecuciones);

        // Y las dos peticiones recibieron exactamente la misma respuesta, byte por byte.
        Assert.Equal(primera.Cuerpo, segunda.Cuerpo);
        Assert.Equal(primera.Estado, segunda.Estado);
        Assert.Equal(StatusCodes.Status200OK, segunda.Estado);

        // Una sola clave guardada, con la respuesta que se repite.
        await using var db = Contexto(_empresa);
        var claves = await db.ClavesIdempotencia.AsNoTracking().ToListAsync();

        Assert.Single(claves);
        Assert.Equal(Ruta, claves[0].Endpoint);
        Assert.Equal(StatusCodes.Status200OK, claves[0].CodigoEstado);
    }

    /// <summary>
    /// La misma clave con otro contenido es un error del cliente. Devolverle la respuesta
    /// guardada sería peor que fallar: creería que se le cobró el paquete que pidió ahora,
    /// cuando lo que se cobró fue el de antes.
    /// </summary>
    [Fact]
    public async Task La_misma_clave_con_otro_cuerpo_se_rechaza()
    {
        const string clave = "clave-reciclada";

        var ejecuciones = 0;

        Task<object?> Endpoint(EndpointFilterInvocationContext _)
        {
            ejecuciones++;
            return Task.FromResult<object?>(Results.Ok(new { compraId = Guid.NewGuid() }));
        }

        await Invocar(clave, """{"paqueteId":"9c1f0a10-0000-4000-8000-000000000001"}""", Endpoint);
        var segunda = await Invocar(clave, """{"paqueteId":"9c1f0a10-0000-4000-8000-000000000002"}""", Endpoint);

        Assert.Equal(1, ejecuciones);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, segunda.Estado);
        Assert.Contains("idempotency-key-reutilizada", segunda.Cuerpo);
    }

    /// <summary>Sin el encabezado no se ejecuta nada: CLAUDE.md §4 lo hace obligatorio.</summary>
    [Fact]
    public async Task Sin_el_encabezado_la_peticion_se_rechaza()
    {
        var ejecuciones = 0;

        Task<object?> Endpoint(EndpointFilterInvocationContext _)
        {
            ejecuciones++;
            return Task.FromResult<object?>(Results.Ok(new { }));
        }

        var respuesta = await Invocar(clave: null, "{}", Endpoint);

        Assert.Equal(0, ejecuciones);
        Assert.Equal(StatusCodes.Status400BadRequest, respuesta.Estado);
        Assert.Contains("idempotency-key-requerida", respuesta.Cuerpo);
    }

    /// <summary>
    /// Dos peticiones con la misma clave <b>a la vez</b>. Es el caso que el orden
    /// «reclamar antes de ejecutar» existe para cubrir: el doble clic real.
    /// </summary>
    [Fact]
    public async Task Dos_peticiones_simultaneas_con_la_misma_clave_solo_ejecutan_una()
    {
        const string clave = "doble-clic";
        const string cuerpo = """{"paqueteId":"9c1f0a10-0000-4000-8000-000000000001"}""";

        var ejecuciones = 0;

        async Task<object?> Endpoint(EndpointFilterInvocationContext _)
        {
            Interlocked.Increment(ref ejecuciones);

            // Alarga la ejecución para que la segunda petición llegue mientras la primera
            // sigue dentro del endpoint, que es donde está la ventana peligrosa.
            await Task.Delay(100);

            return Results.Ok(new { compraId = Guid.NewGuid() });
        }

        var pistoletazo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tareas = Enumerable.Range(0, 2).Select(async _ =>
        {
            await pistoletazo.Task;
            return await Invocar(clave, cuerpo, Endpoint);
        }).ToArray();

        pistoletazo.SetResult();

        var respuestas = await Task.WhenAll(tareas);

        // Lo que no puede pasar bajo ninguna circunstancia: cobrar dos veces.
        Assert.Equal(1, ejecuciones);

        // La que perdió la carrera recibe 200 con la respuesta de la ganadora, o 409 si llegó
        // mientras la otra seguía en curso. Las dos son correctas; lo que importa es que no
        // ejecutó nada.
        Assert.Contains(respuestas, r => r.Estado == StatusCodes.Status200OK);

        await using var db = Contexto(_empresa);
        Assert.Single(await db.ClavesIdempotencia.AsNoTracking().ToListAsync());
    }

    /// <summary>
    /// Corre el filtro sobre una petición fabricada y devuelve la respuesta ya escrita, que es
    /// la única forma de comparar «la misma respuesta» sin depender del tipo concreto de
    /// <c>IResult</c> que devolvió cada camino.
    /// </summary>
    private async Task<(int Estado, string Cuerpo)> Invocar(
        string? clave, string cuerpo, Func<EndpointFilterInvocationContext, Task<object?>> endpoint)
    {
        // Un contexto de base por invocación, como en una petición real: cada una trae el suyo
        // del contenedor de dependencias.
        await using var db = Contexto(_empresa);

        var tenencia = new ContextoEmpresaFijo(_empresa, _cuenta, _usuario);
        var filtro = new FiltroDeIdempotencia(db, tenencia, NullLogger<FiltroDeIdempotencia>.Instance);

        var http = new DefaultHttpContext { RequestServices = Servicios };
        http.Request.Method = HttpMethods.Post;
        http.Request.Path = Ruta;
        http.Request.ContentType = "application/json";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(cuerpo));
        http.Response.Body = new MemoryStream();

        if (clave is not null) http.Request.Headers["Idempotency-Key"] = clave;

        // Reproduce el orden real de la tubería, y no una versión cómoda de él. En un
        // endpoint de verdad pasan dos cosas antes de que corra el filtro: el middleware
        // habilita el rebobinado, y el enlace de modelo lee el cuerpo hasta el final.
        //
        // Sin estas dos líneas la prueba corría sobre un cuerpo intacto y daba por buena una
        // implementación que en el servidor real hasheaba cero bytes en cada petición, con lo
        // que la defensa contra la clave reciclada quedaba inerte. Se descubrió probando
        // contra el servidor levantado, no aquí.
        http.Request.EnableBuffering();
        await new StreamReader(http.Request.Body, leaveOpen: true).ReadToEndAsync();

        var contexto = EndpointFilterInvocationContext.Create(http);

        var resultado = await filtro.InvokeAsync(contexto, ctx => new ValueTask<object?>(endpoint(ctx)));

        // Se ejecuta el IResult para obtener lo que de verdad viajaría por el cable.
        if (resultado is IResult aEscribir) await aEscribir.ExecuteAsync(http);

        http.Response.Body.Position = 0;
        var texto = await new StreamReader(http.Response.Body).ReadToEndAsync();

        return (http.Response.StatusCode, texto);
    }
}
