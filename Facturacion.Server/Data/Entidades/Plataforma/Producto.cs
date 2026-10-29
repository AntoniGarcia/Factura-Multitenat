namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Producto o servicio del catálogo de la cuenta, que comparten sus empresas (AGENTS.md §11).
/// Es la plantilla de un concepto del CFDI: al facturar se copian estos datos al comprobante y
/// ahí se congelan.
///
/// <para><b>Dos claves del SAT y una unidad en texto</b></para>
/// <see cref="ClaveProdServ"/> y <see cref="ClaveUnidad"/> son del catálogo y viajan al XML;
/// nunca se escriben a mano. <see cref="UnidadTexto"/> es lo que el receptor
/// lee en el PDF —«Litro», «Caja con 12»— y no tiene que coincidir con la clave.
/// </summary>
public sealed class Producto : IEntidadDeCuenta
{
    public Guid Id { get; set; }

    public Guid CuentaId { get; set; }

    public Cuenta Cuenta { get; set; } = null!;

    /// <summary>
    /// Empresa desde la que se dio de alta. Es un dato para filtrar el catálogo, no tenencia:
    /// todas las empresas de la cuenta lo ven y lo usan. Nulo si no quedó registro.
    /// </summary>
    public Guid? EmpresaAltaId { get; set; }

    /// <summary>Consecutivo por cuenta, como la clave de cliente: los capturistas la usan para buscar.</summary>
    public int CodigoInterno { get; set; }

    /// <summary>Clave de <c>c_ClaveProdServ</c>. Obligatoria en CFDI 4.0.</summary>
    public required string ClaveProdServ { get; set; }

    /// <summary>Clave de <c>c_ClaveUnidad</c>. Obligatoria en CFDI 4.0.</summary>
    public required string ClaveUnidad { get; set; }

    /// <summary>Unidad tal como la lee el receptor en el PDF.</summary>
    public required string UnidadTexto { get; set; }

    /// <summary>
    /// El nombre del producto («Nombre del producto» en pantalla). Es lo que va al CFDI como
    /// <c>Descripcion</c> del concepto, por eso conserva ese nombre aquí.
    /// </summary>
    public required string Descripcion { get; set; }

    /// <summary>
    /// Código propio de la empresa, como un SKU («ID interno» en pantalla). No es único: precarga
    /// el <c>NoIdentificacion</c> del concepto, que el SAT limita a 100 caracteres sin «|».
    /// </summary>
    public string? IdInterno { get; set; }

    /// <summary>Descripción ampliada del producto («Descripción» en pantalla). Opcional.</summary>
    public string? Detalle { get; set; }

    /// <summary>Precio de venta. Seis decimales de cálculo, dos de presentación.</summary>
    public decimal ValorUnitario { get; set; }

    /// <summary>Peso informativo en kilogramos.</summary>
    public decimal? PesoKg { get; set; }

    /// <summary>Clave de <c>c_ObjetoImp</c>. Decide si el concepto lleva desglose de impuestos.</summary>
    public required string ObjetoImp { get; set; }

    public ICollection<ProductoImpuesto> Impuestos { get; set; } = [];

    public bool Activo { get; set; } = true;

    public DateTime FechaAltaUtc { get; set; }

    public DateTime? FechaModificacionUtc { get; set; }
}

/// <summary>Impuesto del producto con tipo de factor y tasa o cuota. Permite combinar traslados y retenciones; los atajos de la interfaz solo facilitan la captura.</summary>
public sealed class ProductoImpuesto : IEntidadDeCuenta
{
    public Guid Id { get; set; }

    /// <summary>Permite aplicar el filtro global y validar la cuenta también al consultar impuestos sin cargar el producto.</summary>
    public Guid CuentaId { get; set; }

    public Guid ProductoId { get; set; }

    public Producto Producto { get; set; } = null!;

    /// <summary>Clave de <c>c_Impuesto</c>: 001 ISR, 002 IVA, 003 IEPS.</summary>
    public required string Impuesto { get; set; }

    /// <summary>Clave de <c>c_TipoFactor</c>: Tasa, Cuota o Exento.</summary>
    public required string TipoFactor { get; set; }

    /// <summary>
    /// Valor de <c>c_TasaOCuota</c>: 0.160000 para el IVA del 16 %. Es nulo —y solo puede
    /// serlo— cuando el tipo de factor es Exento.
    /// </summary>
    public decimal? TasaOCuota { get; set; }

    /// <summary>Verdadero si es retención; falso si es traslado.</summary>
    public bool EsRetencion { get; set; }
}
