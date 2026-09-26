namespace Facturacion.Server.Infra.Correo;

/// <summary>
/// Sustituto de desarrollo: escribe el correo en el log en vez de enviarlo. Se usa cuando
/// <c>Correo:Servidor</c> no está configurado en <c>Development</c> — no hay por qué exigirle
/// una cuenta SMTP real a quien solo está probando el circuito de correo en su máquina.
/// <para>
/// Nunca se registra fuera de <c>Development</c>: ver <c>InfraestructuraModule</c>.
/// </para>
/// </summary>
public sealed class ServicioDeCorreoConsola(ILogger<ServicioDeCorreoConsola> registro) : IServicioDeCorreo
{
    public Task EnviarAsync(MensajeDeCorreo mensaje, CancellationToken ct)
    {
        var copia = mensaje.CopiaOculta is { Count: > 0 }
            ? " | CCO: " + string.Join(", ", mensaje.CopiaOculta)
            : string.Empty;

        var adjuntos = mensaje.Adjuntos is { Count: > 0 }
            ? " | Adjuntos: " + string.Join(", ", mensaje.Adjuntos.Select(a => $"{a.NombreArchivo} ({a.Contenido.Length} B)"))
            : string.Empty;

        registro.LogInformation(
            "Correo simulado (Correo:Servidor no configurado) → {Destinatarios}{Copia} | Responder a: {ResponderA} | {Asunto}{Adjuntos}\n{Cuerpo}",
            string.Join(", ", mensaje.Para), copia, mensaje.ResponderA ?? "—", mensaje.Asunto, adjuntos, mensaje.CuerpoHtml);

        return Task.CompletedTask;
    }
}
