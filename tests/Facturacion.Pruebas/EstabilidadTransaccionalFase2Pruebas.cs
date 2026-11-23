using System.Threading.Channels;
using Facturacion.Server.Infra.Correo;
using Facturacion.Server.Infra.Seguridad;
using Facturacion.Server.Modules.Plataforma.Timbres;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Facturacion.Pruebas;

public sealed class EstabilidadTransaccionalFase2Pruebas
{
    [Fact]
    public void El_plazo_del_barrido_de_timbres_da_margen_amplio_sobre_la_conciliacion()
    {
        // ConciliacionDeTimbrados opera a partir de los 30 minutos; el barrido debe dar
        // varias pasadas de margen para no devolver timbres aún en conciliación.
        Assert.True(BarridoDeReservasDeTimbre.Plazo >= TimeSpan.FromHours(2));
    }

    [Fact]
    public async Task La_cola_de_recuperacion_no_enumera_cuentas_y_solo_envia_a_destinatarios_elegibles()
    {
        var correo = new CorreoEnMemoria();
        var servicios = new ServiceCollection()
            .AddMemoryCache()
            .AddSingleton<IServicioDeCorreo>(correo);

        await using var proveedor = servicios.BuildServiceProvider();
        using var cola = new ColaDePrueba(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            proveedor.GetRequiredService<IMemoryCache>(),
            correoElegible: "TITULAR@EXAMPLE.INVALID");

        await cola.StartAsync(default);
        try
        {
            using var plazo = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            Assert.True((await cola.SolicitarAsync("DESCONOCIDO@EXAMPLE.INVALID", plazo.Token)).EsExito);
            Assert.True((await cola.SolicitarAsync("INACTIVO@EXAMPLE.INVALID", plazo.Token)).EsExito);
            Assert.True((await cola.SolicitarAsync("TITULAR@EXAMPLE.INVALID", plazo.Token)).EsExito);

            var enviado = await correo.Mensajes.Reader.ReadAsync(plazo.Token);
            Assert.Equal("titular@example.invalid", Assert.Single(enviado.Para));
            Assert.False(correo.Mensajes.Reader.TryRead(out _));
        }
        finally
        {
            await cola.StopAsync(default);
        }
    }

    private sealed class ColaDePrueba(
        IServiceScopeFactory ambitos,
        IMemoryCache memoria,
        string correoElegible)
        : ColaDeCodigosDeRecuperacion(ambitos, memoria, NullLogger.Instance, "prueba", "de prueba")
    {
        private static readonly Guid IdElegible = Guid.NewGuid();

        protected override Task<Destinatario?> BuscarDestinatarioAsync(
            IServiceProvider servicios, string correoNormalizado, CancellationToken ct)
            => Task.FromResult(string.Equals(correoNormalizado, correoElegible, StringComparison.Ordinal)
                ? new Destinatario(IdElegible, correoElegible.ToLowerInvariant(), "v1")
                : null);
    }

    private sealed class CorreoEnMemoria : IServicioDeCorreo
    {
        public Channel<MensajeDeCorreo> Mensajes { get; } = Channel.CreateUnbounded<MensajeDeCorreo>();

        public Task EnviarAsync(MensajeDeCorreo mensaje, CancellationToken ct)
            => Mensajes.Writer.WriteAsync(mensaje, ct).AsTask();
    }
}
