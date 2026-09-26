namespace Facturacion.Shared.Documentos;

/// <summary>Límites del envío por correo. Los aplica el servidor; el Client solo los anticipa.</summary>
public static class ReglasDeEnvio
{
    /// <summary>
    /// Tope de destinatarios por envío. El remitente es el del SaaS: sin tope, el formulario
    /// serviría para mandar correo masivo con nuestra reputación de dominio.
    /// </summary>
    public const int MaximoDestinatarios = 10;

    public const int LongitudMaximaAsunto = 200;

    public const int LongitudMaximaMensaje = 4000;
}

/// <summary>
/// Lo que el diálogo de envío necesita para abrir ya lleno: el panel «Parámetros de envío CFD»
/// de §1.3 del documento funcional, más el historial para reenviar.
/// </summary>
/// <param name="Destinatarios">El correo principal del cliente, si tiene.</param>
/// <param name="CorreoDeLaEmpresa">
/// Destino de la copia oculta («Enviar a cuenta única» del sistema anterior). Nulo si la
/// empresa no tiene correo de contacto.
/// </param>
public sealed record PropuestaDeEnvioDto(
    IReadOnlyList<string> Destinatarios,
    string Asunto,
    string? CorreoDeLaEmpresa,
    string NombreArchivoPdf,
    string NombreArchivoXml,
    IReadOnlyList<EnvioDeCorreoDto> Historial);

/// <param name="IncluirXml">El PDF va siempre; el XML es opcional, como en el sistema anterior.</param>
public sealed record PeticionDeEnvio(
    IReadOnlyList<string> Destinatarios,
    string Asunto,
    string? Mensaje,
    bool IncluirXml,
    bool CopiaALaEmpresa);

public sealed record EnvioDeCorreoDto(
    Guid Id,
    DateTime EnviadoUtc,
    IReadOnlyList<string> Destinatarios,
    string? CopiaOculta,
    string Asunto,
    bool IncluyoXml,
    bool Exitoso,
    string? Error);
