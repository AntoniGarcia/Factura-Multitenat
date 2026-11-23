using System.Text.RegularExpressions;
using System.Threading.Channels;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Correo;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Server.Modules.Plataforma.Auth;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Facturacion.Pruebas;

public sealed class RecuperacionDeClientePruebas : IAsyncLifetime
{
    private readonly string _baseTemporal = "FacturacionPruebasRecuperarCliente_" + Guid.NewGuid().ToString("N");
    private const string Correo = "cliente@example.invalid";
    private const string Anterior = "Anterior de prueba 12";
    private const string Nueva = "Nueva de prueba 34";

    [Fact]
    public async Task Recuperacion_aisla_las_identidades_y_solo_acepta_el_codigo_una_vez()
    {
        var correo = new CorreoCapturado();
        var servicios = new ServiceCollection().AddLogging().AddMemoryCache();
        servicios.AddDataProtection().UseEphemeralDataProtectionProvider();
        servicios.AddIdentityCore<Usuario>(o =>
        {
            o.Password.RequiredLength = 12;
            o.Password.RequireNonAlphanumeric = false;
        }).AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
        servicios.AddSingleton<IServicioDeCorreo>(correo);
        servicios.AddScoped(_ => Abrir());
        servicios.AddScoped<IServicioDeBitacora>(p => new ServicioDeBitacora(p.GetRequiredService<AppDbContext>(),
            ContextoEmpresaFijo.SinEmpresa, ContextoDeOperadorFijo.SinOperador, new HttpContextAccessor()));
        servicios.AddScoped<ServicioDeRefreshTokens>();
        servicios.AddSingleton<ColaDeRecuperacionDeCliente>();
        servicios.AddSingleton<ColaDeRecuperacionDeOperador>();
        servicios.AddScoped<ServicioDeRecuperacionDeCliente>();

        await using var proveedor = servicios.BuildServiceProvider();
        var cliente = await Sembrar(proveedor);
        var colaCliente = proveedor.GetRequiredService<ColaDeRecuperacionDeCliente>();
        var colaOperador = proveedor.GetRequiredService<ColaDeRecuperacionDeOperador>();
        await colaCliente.StartAsync(default);
        await colaOperador.StartAsync(default);
        try
        {
            using var plazo = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            foreach (var noDisponible in new[] { "DESCONOCIDO", "INTERNO", "INACTIVO", "SIN-VERIFICAR" })
                Assert.Equal("recuperacion-no-disponible",
                    (await colaCliente.SolicitarAsync($"{noDisponible}@EXAMPLE.INVALID", plazo.Token)).Error?.Codigo);
            Assert.True((await colaCliente.SolicitarAsync(Correo.ToUpperInvariant(), plazo.Token)).EsExito);
            var codigo = await LeerCodigoAsync("facturación");
            Assert.True((await colaOperador.SolicitarAsync(Correo.ToUpperInvariant(), plazo.Token)).EsExito);
            var codigoOperador = await LeerCodigoAsync("operador");
            var peticion = new PeticionRestablecerContrasena(Correo, codigo, Nueva);

            Assert.Equal("codigo-invalido", (await Restablecer(peticion with { Codigo = codigoOperador })).Error?.Codigo);
            Assert.Equal("contrasena-invalida",
                (await Restablecer(peticion with { ContrasenaNueva = "solominusculas" })).Error?.Codigo);
            var confirmaciones = await Task.WhenAll(Restablecer(peticion), Restablecer(peticion));
            Assert.Single(confirmaciones, r => r.EsExito);
            Assert.Single(confirmaciones, r => r.Error?.Codigo == "codigo-invalido");
            Assert.Equal("codigo-invalido", (await Restablecer(peticion)).Error?.Codigo);

            await using var ambito = proveedor.CreateAsyncScope();
            var usuarios = ambito.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            var guardado = (await usuarios.FindByIdAsync(cliente.ToString()))!;
            Assert.True(await usuarios.CheckPasswordAsync(guardado, Nueva));
            Assert.False(await usuarios.CheckPasswordAsync(guardado, Anterior));
            Assert.Null(guardado.LockoutEnd);
            var db = ambito.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.NotNull((await db.RefreshTokens.SingleAsync(t => t.UsuarioId == cliente)).RevocadoUtc);

            async Task<string> LeerCodigoAsync(string circuito)
            {
                var mensaje = await correo.Mensajes.Reader.ReadAsync(plazo.Token);
                Assert.Equal(Correo, Assert.Single(mensaje.Para));
                Assert.Contains(circuito, mensaje.Asunto);
                var codigoLeido = Regex.Match(mensaje.CuerpoHtml, "<strong>([A-F0-9]{12})</strong>").Groups[1].Value;
                Assert.NotEmpty(codigoLeido);
                return codigoLeido;
            }
        }
        finally
        {
            await colaCliente.StopAsync(default);
            await colaOperador.StopAsync(default);
        }

        async Task<Resultado> Restablecer(PeticionRestablecerContrasena peticion)
        {
            await using var ambito = proveedor.CreateAsyncScope();
            return await ambito.ServiceProvider.GetRequiredService<ServicioDeRecuperacionDeCliente>()
                .RestablecerAsync(peticion, default);
        }
    }

    private static async Task<Guid> Sembrar(IServiceProvider proveedor)
    {
        await using var ambito = proveedor.CreateAsyncScope();
        var db = ambito.ServiceProvider.GetRequiredService<AppDbContext>();
        var usuarios = ambito.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var cuenta = Guid.NewGuid();
        db.Cuentas.Add(new Cuenta { Id = cuenta, Nombre = "Prueba", CorreoContacto = Correo });
        db.OperadoresPlataforma.Add(new OperadorPlataforma
        {
            Id = Guid.NewGuid(), Nombre = "Operador", Correo = Correo, CorreoNormalizado = Correo.ToUpperInvariant(),
            HashContrasena = new PasswordHasher<OperadorPlataforma>().HashPassword(null!, Anterior)
        });
        await db.SaveChangesAsync();

        var cliente = await Crear(Correo);
        await Crear("interno@example.invalid", interno: true);
        await Crear("inactivo@example.invalid", activo: false);
        var correoCambiado = await Crear("anterior@example.invalid");
        Assert.True((await usuarios.SetEmailAsync(correoCambiado, "sin-verificar@example.invalid")).Succeeded);
        Assert.False(correoCambiado.EmailConfirmed);
        ambito.ServiceProvider.GetRequiredService<ServicioDeRefreshTokens>()
            .CrearFamilia(cliente.Id, TimeSpan.FromHours(12), null, null);
        await db.SaveChangesAsync();
        return cliente.Id;

        async Task<Usuario> Crear(string email, bool interno = false, bool activo = true)
        {
            var usuario = new Usuario
            {
                Id = Guid.NewGuid(), CuentaId = cuenta, Nombre = "Usuario de prueba", Email = email, UserName = email,
                EmailConfirmed = true, Activo = activo, CreadoPorAdministrador = interno,
                LockoutEnd = DateTimeOffset.UtcNow.AddHours(1), AccessFailedCount = 2
            };
            Assert.True((await usuarios.CreateAsync(usuario, Anterior)).Succeeded);
            return usuario;
        }
    }

    public async Task InitializeAsync()
    {
        await using var db = Abrir();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = Abrir();
        await db.Database.EnsureDeletedAsync();
    }

    private AppDbContext Abrir()
    {
        var contexto = ContextoEmpresaFijo.SinEmpresa;
        var opciones = new DbContextOptionsBuilder<AppDbContext>().Configurar(
            $"Server=.;Database={_baseTemporal};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False", contexto).Options;
        return new AppDbContext(opciones, contexto);
    }

    private sealed class CorreoCapturado : IServicioDeCorreo
    {
        public Channel<MensajeDeCorreo> Mensajes { get; } = Channel.CreateUnbounded<MensajeDeCorreo>();

        public Task EnviarAsync(MensajeDeCorreo mensaje, CancellationToken ct)
            => Mensajes.Writer.WriteAsync(mensaje, ct).AsTask();
    }
}
