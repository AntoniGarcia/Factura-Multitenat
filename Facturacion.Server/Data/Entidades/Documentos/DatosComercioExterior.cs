namespace Facturacion.Server.Data.Entidades.Documentos;

public sealed class DatosComercioExterior : IEntidadDeEmpresa
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid ComprobanteId { get; set; }
    public Comprobante Comprobante { get; set; } = null!;
    public required string Contenido { get; set; }
}
