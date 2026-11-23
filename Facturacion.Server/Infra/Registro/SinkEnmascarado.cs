using System.Text;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Facturacion.Server.Infra.Registro;

/// <summary>
/// Envuelve un destino de registro y enmascara el <b>texto de la plantilla</b> y las
/// <b>excepciones</b> antes de escribirlos.
/// <para>
/// Existe por dos casos que ningún enriquecedor puede cubrir: cuando alguien escribe
/// <c>_registro.LogInformation($"RFC {rfc}")</c> con interpolación de C# y cuando un dato
/// sensible aparece dentro de <c>Exception.Message</c> o de una <c>InnerException</c>.
/// </para>
/// </summary>
public sealed class SinkEnmascarado(ILogEventSink interno) : ILogEventSink, IDisposable
{
    private static readonly MessageTemplateParser Analizador = new();

    public void Emit(LogEvent evento)
    {
        var original = evento.MessageTemplate.Text;
        var enmascarado = EnmascaradorDeTextoSensible.Enmascarar(original);
        var excepcion = evento.Exception is null ? null : EnmascararExcepcion(evento.Exception);

        interno.Emit(enmascarado == original && excepcion is null
            ? evento
            : new LogEvent(
                evento.Timestamp,
                evento.Level,
                excepcion,
                enmascarado == original ? evento.MessageTemplate : Analizador.Parse(enmascarado),
                evento.Properties.Select(p => new LogEventProperty(p.Key, p.Value)),
                evento.TraceId ?? default,
                evento.SpanId ?? default));
    }

    public void Dispose() => (interno as IDisposable)?.Dispose();

    private static Exception EnmascararExcepcion(Exception original)
    {
        var internas = original is AggregateException agregada && agregada.InnerExceptions.Count > 1
            ? agregada.InnerExceptions.Select(EnmascararExcepcion).ToArray()
            : null;
        var interna = internas is null && original.InnerException is not null
            ? EnmascararExcepcion(original.InnerException)
            : internas?[0];

        return new ExcepcionEnmascarada(
            original.GetType().FullName ?? original.GetType().Name,
            EnmascaradorDeTextoSensible.Enmascarar(original.Message),
            original.StackTrace is null ? null : EnmascaradorDeTextoSensible.Enmascarar(original.StackTrace),
            interna,
            internas);
    }

    private sealed class ExcepcionEnmascarada(
        string tipoOriginal,
        string mensajeEnmascarado,
        string? pilaEnmascarada,
        Exception? interna,
        Exception[]? internas) : Exception(mensajeEnmascarado, interna)
    {
        public override string? StackTrace => pilaEnmascarada;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(tipoOriginal).Append(": ").Append(Message);
            if (internas is { Length: > 1 })
            {
                for (var i = 0; i < internas.Length; i++)
                {
                    sb.AppendLine()
                      .Append(" ---> (Inner Exception #").Append(i).Append(") ")
                      .Append(internas[i])
                      .Append("<---");
                }
            }
            else if (InnerException is not null)
            {
                sb.AppendLine().Append(" ---> ").Append(InnerException)
                  .AppendLine().Append("   --- End of inner exception stack trace ---");
            }
            if (!string.IsNullOrEmpty(pilaEnmascarada))
                sb.AppendLine().Append(pilaEnmascarada);
            return sb.ToString();
        }
    }
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
