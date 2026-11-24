namespace Facturacion.Shared.Documentos;

// El servidor los aplica; el Client solo los anticipa.
public static class ReglasDeEnvio
{
    // Evita usar el formulario para correo masivo.
    public const int MaximoDestinatarios = 10;

    public const int LongitudMaximaAsunto = 200;

    public const int LongitudMaximaMensaje = 4000;
}

// CorreoDeLaEmpresa: destino de la copia oculta; nulo si la empresa no tiene correo de contacto.
public sealed record PropuestaDeEnvioDto(
    IReadOnlyList<string> Destinatarios,
    string Asunto,
    string? CorreoDeLaEmpresa,
    string NombreArchivoPdf,
    string NombreArchivoXml,
    IReadOnlyList<EnvioDeCorreoDto> Historial);

// El PDF va siempre; el XML es opcional.
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
