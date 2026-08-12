using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Facturacion.Server.Infra.Registro;

/// <summary>
/// Envuelve un destino de registro y enmascara el <b>texto de la plantilla</b> antes de
/// escribirlo.
/// <para>
/// Existe por un caso que ningún enriquecedor puede cubrir: cuando alguien escribe
/// <c>_registro.LogInformation($"RFC {rfc}")</c> con interpolación de C#, el RFC queda
/// incrustado en la plantilla del mensaje y deja de ser una propiedad. Sin este envoltorio,
/// ese RFC llega íntegro al archivo de log.
/// </para>
/// </summary>
public sealed class SinkEnmascarado(ILogEventSink interno) : ILogEventSink, IDisposable
{
    private static readonly MessageTemplateParser Analizador = new();

    public void Emit(LogEvent evento)
    {
        var original = evento.MessageTemplate.Text;
        var enmascarado = EnmascaradorDeTextoSensible.Enmascarar(original);

        interno.Emit(enmascarado == original
            ? evento
            : new LogEvent(
                evento.Timestamp,
                evento.Level,
                evento.Exception,
                Analizador.Parse(enmascarado),
                evento.Properties.Select(p => new LogEventProperty(p.Key, p.Value)),
                evento.TraceId ?? default,
                evento.SpanId ?? default));
    }

    public void Dispose() => (interno as IDisposable)?.Dispose();
}

public static class SinkEnmascaradoExtensiones
{
    /// <summary>
    /// Envuelve uno o varios destinos con el enmascarado. Se configura en código y no desde
    /// <c>appsettings</c> a propósito: el enmascarado no debe poder apagarse cambiando un
    /// archivo de configuración.
    /// </summary>
    public static LoggerConfiguration Enmascarado(
        this LoggerSinkConfiguration configuracion,
        Action<LoggerSinkConfiguration> configurarDestino)
        => configuracion.Sink(
            LoggerSinkConfiguration.Wrap(destino => new SinkEnmascarado(destino), configurarDestino));
}
