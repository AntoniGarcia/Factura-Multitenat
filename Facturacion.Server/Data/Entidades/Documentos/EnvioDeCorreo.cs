namespace Facturacion.Server.Data.Entidades.Documentos;

/// <summary>
/// Un envío de comprobante por correo, con éxito o sin él. Es lo que permite contestar «¿a quién
/// se le mandó y cuándo?» sin buscar en el buzón del SaaS, y de donde el listado toma los
/// destinatarios para reenviar.
///
/// <para>
/// Se guarda también el envío fallido: un cliente que dice no haber recibido su factura merece
/// una respuesta mejor que la ausencia de un renglón.
/// </para>
/// </summary>
public sealed class EnvioDeCorreo : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid ComprobanteId { get; set; }

    public Comprobante Comprobante { get; set; } = null!;

    /// <summary>Direcciones separadas por <c>;</c>, tal como se mandaron.</summary>
    public required string Destinatarios { get; set; }

    /// <summary>Si fue copia oculta a la cuenta de la empresa, la dirección que se usó.</summary>
    public string? CopiaOculta { get; set; }

    public required string Asunto { get; set; }

    public bool IncluyoXml { get; set; }

    public bool Exitoso { get; set; }

    /// <summary>
    /// Código corto del fallo, para soporte. El detalle de la excepción va al log con el
    /// <c>traceId</c>, no aquí: esta columna llega a la pantalla.
    /// </summary>
    public string? Error { get; set; }

    public Guid EnviadoPorUsuarioId { get; set; }

    public DateTime EnviadoUtc { get; set; }
}
