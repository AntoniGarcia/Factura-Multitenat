namespace Facturacion.Server.Data.Entidades.Documentos;

// El CFDI timbrado tal como lo devolvió el PAC. Tabla aparte para que listar o leer el
// comprobante no cargue el XML.
public sealed class XmlDeComprobante : IEntidadDeEmpresa
{
    public Guid ComprobanteId { get; set; }
    public Guid EmpresaId { get; set; }
    public required string Xml { get; set; }
    public DateTime GuardadoUtc { get; set; }
}
