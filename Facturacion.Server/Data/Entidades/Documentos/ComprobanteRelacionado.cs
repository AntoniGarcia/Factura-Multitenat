namespace Facturacion.Server.Data.Entidades.Documentos;

/// <summary>
/// Un CFDI al que este comprobante hace referencia: la nota de crédito que corrige a una
/// factura, la factura que sustituye a otra cancelada, el comprobante que se paga.
///
/// <para>
/// El relacionado se guarda por su <b>UUID</b> y no por una llave foránea a
/// <c>Comprobante</c>, y no por descuido: se puede relacionar un CFDI emitido en otro
/// sistema, o antes de esta migración, que no existe como renglón en esta base. Guardar el
/// UUID es lo único que funciona en los dos casos, y es además lo que viaja al XML.
/// </para>
/// </summary>
public sealed class ComprobanteRelacionado : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid ComprobanteId { get; set; }

    public Comprobante Comprobante { get; set; } = null!;

    /// <summary>Clave de <c>c_TipoRelacion</c>: 01 nota de crédito, 04 sustitución, etc.</summary>
    public required string TipoRelacion { get; set; }

    /// <summary>Folio fiscal del comprobante relacionado.</summary>
    public Guid UuidRelacionado { get; set; }
}
