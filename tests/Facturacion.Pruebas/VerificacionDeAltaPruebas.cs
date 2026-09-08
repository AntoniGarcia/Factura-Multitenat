using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Correo;
using Facturacion.Server.Infra.Seguridad;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Plataforma.Auth;
using Facturacion.Shared.Plataforma;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Facturacion.Pruebas;

/// <summary>
/// El alta va en dos pasos y la cuenta nace en el segundo. Lo que se prueba aquí es
/// justamente eso: que el primer paso <b>no crea nada</b> y que el código sirve una sola vez.
///
/// <para>
/// Si el código valiera dos veces, o si la cuenta existiera antes de canjearlo, todo el
/// circuito sobraría: la contraseña saldría hacia un buzón que nadie ha demostrado controlar,
/// que es exactamente lo que se quería evitar.
/// </para>
/// <para>
/// Se prueba contra SQL Server, con Identity montado como lo monta la aplicación: lo que
/// interesa comprobar es el estado que <b>quedó guardado</b>, no lo que pasó en memoria.
/// </para>
/// </summary>
public sealed class VerificacionDeAltaPruebas : IAsyncLifetime
{
    private const string Conexion =
        "Server=.;Database=FacturacionPruebasAlta;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    private const string Correo = "alta@ejemplo.mx";

    /// <summary>
    /// Se queda con lo que se habría mandado. El código solo existe en el cuerpo del correo
    /// —en la base está con hash— así que esta es la única forma de leerlo, y es también la
    /// forma en que lo lee quien se registra.
    /// </summary>
    private sealed class CorreoDeMemoria : IServicioDeCorreo
    {
        public List<(string Destinatario, string Asunto, string Cuerpo)> Enviados { get; } = [];

        public Task EnviarAsync(
            string destinatario, string asunto, string cuerpoHtml, CancellationToken ct, string? responderA = null)
        {
            Enviados.Add((destinatario, asunto, cuerpoHtml));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Plantillas fijas con los valores predeterminados: la prueba extrae el código del
    /// cuerpo del correo, que es de donde lo lee una persona.
    /// </summary>
    private sealed class MensajesDeRegistroFijos : IOptionsMonitor<OpcionesDeMensajes>
    {
        public OpcionesDeMensajes CurrentValue { get; } = new();

        public OpcionesDeMensajes Get(string? nombre) => CurrentValue;

        public IDisposable? OnChange(Action<OpcionesDeMensajes, string?> oyente) => null;
    }

    private readonly CorreoDeMemoria _correo = new();

    private static AppDbContext Contexto()
    {
        var tenencia = new ContextoEmpresaFijo();
        var opciones = new DbContextOptionsBuilder<AppDbContext>().Configurar(Conexion, tenencia).Options;

        return new AppDbContext(opciones, tenencia);
    }

    /// <summary>
    /// Identity montado igual que en la aplicación —mismas reglas de contraseña, mismo
    /// almacén— para que la contraseña que genera el servicio se valide con el criterio real
    /// y no con uno más blando inventado para la prueba.
    /// </summary>
    private static UserManager<Usuario> Usuarios(AppDbContext baseDeDatos)
    {
        var servicios = new ServiceCollection();

        servicios.AddLogging();
        servicios.AddSingleton(baseDeDatos);

        servicios
            .AddIdentityCore<Usuario>(opciones =>
            {
                opciones.User.RequireUniqueEmail = true;

                opciones.Password.RequiredLength = 12;
                opciones.Password.RequireDigit = true;
                opciones.Password.RequireLowercase = true;
                opciones.Password.RequireUppercase = true;
                opciones.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        return servicios.BuildServiceProvider().GetRequiredService<UserManager<Usuario>>();
    }

    private ServicioDeRegistro Servicio(AppDbContext baseDeDatos)
    {
        var bitacora = new ServicioDeBitacora(
            baseDeDatos, new ContextoEmpresaFijo(), ContextoDeOperadorFijo.SinOperador, new HttpContextAccessor());

        return new ServicioDeRegistro(
            baseDeDatos,
            Usuarios(baseDeDatos),
            new PasswordHasher<AltaPendiente>(),
            _correo,
            bitacora,
            new ControlDeIntentos(new MemoryCache(new MemoryCacheOptions())),
            new MensajesDeRegistroFijos(),
            NullLogger<ServicioDeRegistro>.Instance);
    }

    public async Task InitializeAsync()
    {
        await using var baseDeDatos = Contexto();
        await baseDeDatos.Database.EnsureDeletedAsync();
        await baseDeDatos.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var baseDeDatos = Contexto();
        await baseDeDatos.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task El_alta_no_crea_la_cuenta_hasta_que_el_codigo_vuelve()
    {
        string codigo;

        // Paso uno: pedir el código.
        await using (var baseDeDatos = Contexto())
        {
            var pedido = await Servicio(baseDeDatos).RegistrarAsync(
                new PeticionRegistro(Correo, "Quien Se Registra", "Despacho de prueba"),
                "127.0.0.1", CancellationToken.None);

            Assert.True(pedido.EsExito);
        }

        // Nada creado todavía: ni cuenta ni usuario, solo el alta esperando.
        await using (var baseDeDatos = Contexto())
        {
            Assert.False(await baseDeDatos.Users.AnyAsync(u => u.Email == Correo));
            Assert.False(await baseDeDatos.Cuentas.AnyAsync());
            Assert.True(await baseDeDatos.AltasPendientes.AnyAsync(a => a.Correo == Correo));
        }

        var mensaje = Assert.Single(_correo.Enviados);
        Assert.Equal(Correo, mensaje.Destinatario);

        codigo = ExtraerCodigo(mensaje.Cuerpo);

        // Un código equivocado no crea nada y gasta un intento.
        await using (var baseDeDatos = Contexto())
        {
            var fallido = await Servicio(baseDeDatos).VerificarAsync(
                new PeticionVerificarAlta(Correo, SiguienteCodigo(codigo)), "127.0.0.1", CancellationToken.None);

            Assert.True(fallido.EsFallo);
            Assert.False(await baseDeDatos.Users.AnyAsync(u => u.Email == Correo));

            var alta = await baseDeDatos.AltasPendientes.SingleAsync(a => a.Correo == Correo);
            Assert.Equal(1, alta.Intentos);
        }

        // Paso dos: el código bueno. Aquí sí nace la cuenta.
        await using (var baseDeDatos = Contexto())
        {
            var verificado = await Servicio(baseDeDatos).VerificarAsync(
                new PeticionVerificarAlta(Correo, codigo), "127.0.0.1", CancellationToken.None);

            Assert.True(verificado.EsExito);
        }

        await using (var baseDeDatos = Contexto())
        {
            var usuario = await baseDeDatos.Users.SingleAsync(u => u.Email == Correo);

            // Confirmado de verdad: hizo falta leer el código del buzón para llegar hasta aquí.
            Assert.True(usuario.EmailConfirmed);
            Assert.Single(await baseDeDatos.Cuentas.ToListAsync());

            var alta = await baseDeDatos.AltasPendientes.SingleAsync(a => a.Correo == Correo);
            Assert.NotNull(alta.ConsumidoUtc);
        }

        // La contraseña sale en un correo aparte, después del código y no antes.
        Assert.Equal(2, _correo.Enviados.Count);
        Assert.Contains("Contraseña", _correo.Enviados[1].Cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task El_mismo_codigo_no_sirve_dos_veces()
    {
        await using (var baseDeDatos = Contexto())
        {
            await Servicio(baseDeDatos).RegistrarAsync(
                new PeticionRegistro(Correo, "Quien Se Registra", null), "127.0.0.1", CancellationToken.None);
        }

        var codigo = ExtraerCodigo(_correo.Enviados[0].Cuerpo);

        await using (var baseDeDatos = Contexto())
        {
            var primera = await Servicio(baseDeDatos).VerificarAsync(
                new PeticionVerificarAlta(Correo, codigo), "127.0.0.1", CancellationToken.None);

            Assert.True(primera.EsExito);
        }

        // El segundo canje es el que importa: sin esto, un doble clic —o alguien con el
        // código en la mano— crearía una segunda cuenta con el mismo correo.
        await using (var baseDeDatos = Contexto())
        {
            var segunda = await Servicio(baseDeDatos).VerificarAsync(
                new PeticionVerificarAlta(Correo, codigo), "127.0.0.1", CancellationToken.None);

            Assert.True(segunda.EsFallo);
            Assert.Single(await baseDeDatos.Users.Where(u => u.Email == Correo).ToListAsync());
            Assert.Single(await baseDeDatos.Cuentas.ToListAsync());
        }
    }

    /// <summary>
    /// Saca los seis dígitos del cuerpo del correo, que es de donde los saca una persona.
    /// </summary>
    private static string ExtraerCodigo(string cuerpo)
    {
        var codigo = System.Text.RegularExpressions.Regex.Match(cuerpo, @"\b(\d{6})\b");

        Assert.True(codigo.Success, "El correo del código tiene que llevar los seis dígitos.");

        return codigo.Groups[1].Value;
    }

    /// <summary>Un código distinto del bueno, para probar el camino del fallo.</summary>
    private static string SiguienteCodigo(string codigo)
        => ((int.Parse(codigo) + 1) % 1_000_000).ToString("D6");
}
