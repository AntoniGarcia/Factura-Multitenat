using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Facturacion.Server.Infra.Correo;
using Facturacion.Shared.Comun;
using Microsoft.Extensions.Caching.Memory;

namespace Facturacion.Server.Infra.Seguridad;

public abstract class ColaDeCodigosDeRecuperacion(
    IServiceScopeFactory ambitos,
    IMemoryCache memoria,
    ILogger registro,
    string circuito,
    string acceso) : BackgroundService
{
    private const int MinutosDeVigencia = 15;
    private static readonly TimeSpan EsperaEntreCorreos = TimeSpan.FromMinutes(1);
    private readonly Channel<string> _solicitudes = Channel.CreateBounded<string>(
        new BoundedChannelOptions(32) { SingleReader = true });

    protected sealed record Destinatario(Guid Id, string Correo, string VersionDeCredencial);
    internal sealed record CodigoEmitido(Guid Id, string VersionDeCredencial, byte[] Huella, DateTime EmitidoUtc);

    protected abstract Task<Destinatario?> BuscarDestinatarioAsync(IServiceProvider servicios,
        string correoNormalizado, CancellationToken ct);

    public Task<Resultado> SolicitarAsync(string correoNormalizado, CancellationToken ct)
    {
        // Responde igual exista o no el destinatario para evitar enumeración de cuentas;
        // el worker de fondo descarta silenciosamente los correos que no aplican.
        var resultado = _solicitudes.Writer.TryWrite(correoNormalizado)
            ? Resultado.Exito()
            : ErrorNegocio.LimiteExcedido("recuperacion-ocupada", "Vuelve a solicitar el código en un minuto.");

        return Task.FromResult(resultado);
    }

    internal CodigoEmitido? Validar(string correoNormalizado, string codigo)
    {
        var emitido = memoria.Get<CodigoEmitido>(Clave(correoNormalizado));
        return emitido is not null && CryptographicOperations.FixedTimeEquals(emitido.Huella, Huella(codigo))
            ? emitido
            : null;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var correoNormalizado in _solicitudes.Reader.ReadAllAsync(ct))
        {
            using var plazo = CancellationTokenSource.CreateLinkedTokenSource(ct);
            plazo.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                await EnviarAsync(correoNormalizado, plazo.Token);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                registro.LogWarning("No se pudo procesar un correo de recuperación de {Circuito}.", circuito);
            }
        }
    }

    private async Task EnviarAsync(string correoNormalizado, CancellationToken ct)
    {
        var clave = Clave(correoNormalizado);
        if (memoria.Get<CodigoEmitido>(clave) is { } anterior &&
            DateTime.UtcNow - anterior.EmitidoUtc < EsperaEntreCorreos)
            return;

        await using var ambito = ambitos.CreateAsyncScope();
        var destinatario = await BuscarDestinatarioAsync(ambito.ServiceProvider, correoNormalizado, ct);
        if (destinatario is null) return;

        var codigo = Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
        memoria.Set(clave, new CodigoEmitido(destinatario.Id, destinatario.VersionDeCredencial, Huella(codigo), DateTime.UtcNow),
            TimeSpan.FromMinutes(MinutosDeVigencia));

        var correo = ambito.ServiceProvider.GetRequiredService<IServicioDeCorreo>();
        await correo.EnviarAsync(new MensajeDeCorreo([destinatario.Correo], $"Recupera tu acceso {acceso}",
            $"""
            <p>Escribe este código en la pantalla que abriste para recuperar tu acceso {acceso}.</p>
            <p><strong>{codigo}</strong></p>
            <p>El código caduca en {MinutosDeVigencia} minutos. Ahí podrás elegir una nueva contraseña.</p>
            <p>Si no solicitaste el cambio, puedes ignorar este correo.</p>
            """, EsConfidencial: true), ct);
    }

    private string Clave(string correoNormalizado) => $"recuperacion:{circuito}:{correoNormalizado}";

    private static byte[] Huella(string codigo) => SHA256.HashData(Encoding.UTF8.GetBytes(codigo));
}
