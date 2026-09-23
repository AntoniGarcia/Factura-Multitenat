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
    public Task EnviarAsync(
        string destinatario, string asunto, string cuerpoHtml, CancellationToken ct,
        string? responderA = null, IReadOnlyList<AdjuntoDeCorreo>? adjuntos = null)
    {
        var listaAdjuntos = adjuntos is { Count: > 0 }
            ? " | Adjuntos: " + string.Join(", ", adjuntos.Select(a => a.NombreArchivo))
            : string.Empty;

        registro.LogInformation(
            "Correo simulado (Correo:Servidor no configurado) → {Destinatario} | {Asunto}{Adjuntos}\n{Cuerpo}",
            destinatario, asunto, listaAdjuntos, cuerpoHtml);

        return Task.CompletedTask;
    }
}