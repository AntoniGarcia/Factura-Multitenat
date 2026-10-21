namespace Facturacion.Server.Data.Entidades.Documentos;

/// <summary>Concepto con copia de los datos facturados. Incluye EmpresaId para que el filtro global también proteja las consultas directas.</summary>
public sealed class Concepto : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid ComprobanteId { get; set; }

    public Comprobante Comprobante { get; set; } = null!;

    /// <summary>Posición del renglón, empezando en 1. El XML respeta este orden.</summary>
    public int Orden { get; set; }

    /// <summary>Solo trazabilidad hacia el catálogo. Ningún dato del renglón se lee de aquí.</summary>
    public Guid? ProductoId { get; set; }

    /// <summary>Clave de <c>c_ClaveProdServ</c>.</summary>
    public required string ClaveProdServ { get; set; }

    /// <summary>Clave de <c>c_ClaveUnidad</c>.</summary>
    public required string ClaveUnidad { get; set; }

    /// <summary>Unidad como la lee el receptor en el PDF; no tiene que coincidir con la clave.</summary>
    public string? UnidadTexto { get; set; }

    /// <summary>Código interno del producto en la empresa, si venía de catálogo.</summary>
    public string? NoIdentificacion { get; set; }

    public required string Descripcion { get; set; }

    public decimal Cantidad { get; set; }

    public decimal ValorUnitario { get; set; }

    /// <summary>Cantidad × valor unitario, a seis decimales.</summary>
    public decimal Importe { get; set; }

    /// <summary>Se resta del importe antes de calcular la base gravable.</summary>
    public decimal Descuento { get; set; }

    /// <summary>Clave de <c>c_ObjetoImp</c>: 01 no objeto, 02 sí objeto, 03 sí objeto sin desglose.</summary>
    public required string ObjetoImp { get; set; }

    public List<ImpuestoConcepto> Impuestos { get; set; } = [];
}
