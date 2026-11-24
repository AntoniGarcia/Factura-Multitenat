namespace Facturacion.Server.Data.Entidades.Documentos;

// Un envío por correo, exitoso o fallido: responde a quién y cuándo se mandó, y de aquí
// toma el listado los destinatarios para reenviar.
public sealed class EnvioDeCorreo : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid ComprobanteId { get; set; }

    public Comprobante Comprobante { get; set; } = null!;

    // Separadas por «;», tal como se mandaron.
    public required string Destinatarios { get; set; }

    // La dirección de la copia oculta a la empresa, si la hubo.
    public string? CopiaOculta { get; set; }

    public required string Asunto { get; set; }

    public bool IncluyoXml { get; set; }

    public bool Exitoso { get; set; }

    // Código corto del fallo; llega a la pantalla, así que el detalle va al log.
    public string? Error { get; set; }

    public Guid EnviadoPorUsuarioId { get; set; }

    public DateTime EnviadoUtc { get; set; }
}
