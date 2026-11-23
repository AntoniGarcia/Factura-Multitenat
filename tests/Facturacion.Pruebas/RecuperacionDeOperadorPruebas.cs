using System.Text.RegularExpressions;
using System.Threading.Channels;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Correo;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Shared.Operador;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Facturacion.Pruebas;

public sealed class RecuperacionDeOperadorPruebas : IAsyncLifetime
{
    private readonly string _nombreBase = "FacturacionPruebasRecuperacion_" + Guid.NewGuid().ToString("N");
    private readonly PasswordHasher<OperadorPlataforma> _hasher = new();
    private readonly CorreoCapturado _correo = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Recupera_operador_activo_una_sola_vez_y_revoca_sus_refresh_tokens(bool principal)
    {
        var operador = NuevoOperador("operador@example.invalid", principal, activo: true);
        var inactivo = NuevoOperador("inactivo@example.invalid", principal: false, activo: false);
        await using (var db = Abrir())
        {
            db.OperadoresPlataforma.AddRange(operador, inactivo);
            Refrescos(db).CrearFamilia(operador.Id, TimeSpan.FromHours(12), null, null);
            Refrescos(db).CrearFamilia(inactivo.Id, TimeSpan.FromHours(12), null, null);
            await db.SaveChangesAsync();
        }

        var servicios = new ServiceCollection();
        servicios.AddScoped(_ => Abrir());
        servicios.AddSingleton<IServicioDeCorreo>(_correo);
        servicios.AddMemoryCache();
        await using var proveedor = servicios.BuildServiceProvider();
        using var cola = new ColaDeRecuperacionDeOperador(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            proveedor.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
            NullLogger<ColaDeRecuperacionDeOperador>.Instance);
        await cola.StartAsync(default);
        try
        {
            using var plazo = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            Assert.Equal("recuperacion-no-disponible",
                (await cola.SolicitarAsync(inactivo.CorreoNormalizado, plazo.Token)).Error?.Codigo);
            Assert.Equal("recuperacion-no-disponible",
                (await cola.SolicitarAsync("DESCONOCIDO@EXAMPLE.INVALID", plazo.Token)).Error?.Codigo);
            Assert.True((await cola.SolicitarAsync(operador.CorreoNormalizado, plazo.Token)).EsExito);

            var correo = await _correo.Mensajes.Reader.ReadAsync(plazo.Token);
            Assert.Equal(operador.Correo, Assert.Single(correo.Para));
            Assert.True(correo.EsConfidencial);
            var codigo = Regex.Match(correo.CuerpoHtml, "<strong>([A-F0-9]{12})</strong>").Groups[1].Value;
            Assert.NotEmpty(codigo);

            const string nueva = "Nueva contraseña de prueba";
            var peticion = new PeticionRestablecerContrasenaOperador(operador.Correo, codigo, nueva);
            Assert.Equal("codigo-invalido", (await Restablecer(peticion with { Correo = inactivo.Correo })).Error?.Codigo);
            Assert.Equal("codigo-invalido", (await Restablecer(peticion with { Codigo = "ZZZZZZZZZZZZ" })).Error?.Codigo);
            Assert.DoesNotContain(nueva, correo.CuerpoHtml);

            var resultados = await Task.WhenAll(Restablecer(peticion), Restablecer(peticion with { Codigo = codigo.ToLowerInvariant() }));
            Assert.Single(resultados, r => r.EsExito);
            Assert.Single(resultados, r => r.Error?.Codigo == "codigo-invalido");
            Assert.Equal("codigo-invalido", (await Restablecer(peticion)).Error?.Codigo);

            await using var db = Abrir();
            var guardado = await db.OperadoresPlataforma.SingleAsync(o => o.Id == operador.Id);
            Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(guardado, guardado.HashContrasena, nueva));
            Assert.Equal(PasswordVerificationResult.Failed, _hasher.VerifyHashedPassword(guardado, guardado.HashContrasena, "Contraseña anterior de prueba"));
            Assert.Null(guardado.BloqueadoHastaUtc);
            Assert.Equal(0, guardado.AccesosFallidos);
            Assert.NotNull((await db.RefreshTokensOperador.SingleAsync(t => t.OperadorId == operador.Id)).RevocadoUtc);
            Assert.Null((await db.RefreshTokensOperador.SingleAsync(t => t.OperadorId == inactivo.Id)).RevocadoUtc);
            var evento = await db.Bitacora.IgnoreQueryFilters().SingleAsync(b => b.Accion == AccionesDeBitacora.ContrasenaCambiada);
            Assert.Equal(operador.Id, evento.OperadorId);
            Assert.DoesNotContain(codigo, evento.ValorNuevo!);
            Assert.DoesNotContain(nueva, evento.ValorNuevo!);

            async Task<Facturacion.Shared.Comun.Resultado> Restablecer(PeticionRestablecerContrasenaOperador solicitud)
            {
                await using var contexto = Abrir();
                return await new ServicioDeRecuperacionDeOperador(contexto, cola, _hasher, Refrescos(contexto), Bitacora(contexto))
                    .RestablecerAsync(solicitud, default);
            }
        }
        finally { await cola.StopAsync(default); }
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

    private OperadorPlataforma NuevoOperador(string correo, bool principal, bool activo)
    {
        var operador = new OperadorPlataforma
        {
            Id = Guid.NewGuid(), Nombre = "Operador de prueba", Correo = correo, CorreoNormalizado = correo.ToUpperInvariant(),
            HashContrasena = "", EsPrincipal = principal, Activo = activo, FechaAltaUtc = DateTime.UtcNow,
            BloqueadoHastaUtc = DateTime.UtcNow.AddHours(1), AccesosFallidos = 3, BloqueosConsecutivos = 1
        };
        operador.HashContrasena = _hasher.HashPassword(operador, "Contraseña anterior de prueba");
        return operador;
    }

    private AppDbContext Abrir()
    {
        var contexto = ContextoEmpresaFijo.SinEmpresa;
        var opciones = new DbContextOptionsBuilder<AppDbContext>().Configurar(
            $"Server=.;Database={_nombreBase};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False", contexto).Options;
        return new AppDbContext(opciones, contexto);
    }

    private static ServicioDeBitacora Bitacora(AppDbContext db)
        => new(db, ContextoEmpresaFijo.SinEmpresa, ContextoDeOperadorFijo.SinOperador, new HttpContextAccessor());

    private static ServicioDeRefreshTokensDeOperador Refrescos(AppDbContext db)
        => new(db, Bitacora(db), NullLogger<ServicioDeRefreshTokensDeOperador>.Instance);

    private sealed class CorreoCapturado : IServicioDeCorreo
    {
        public Channel<MensajeDeCorreo> Mensajes { get; } = Channel.CreateUnbounded<MensajeDeCorreo>();

        public Task EnviarAsync(MensajeDeCorreo mensaje, CancellationToken ct)
            => Mensajes.Writer.WriteAsync(mensaje, ct).AsTask();
    }
}
