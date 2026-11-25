namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Producto o servicio del catálogo de la cuenta, que comparten sus empresas.
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

    // Empresa desde la que se dio de alta: sirve para filtrar, no es tenencia.
    public Guid? EmpresaAltaId { get; set; }

    // Consecutivo por cuenta; los capturistas lo usan para buscar.
    public int CodigoInterno { get; set; }

    /// <summary>Clave de <c>c_ClaveProdServ</c>. Obligatoria en CFDI 4.0.</summary>
    public required string ClaveProdServ { get; set; }

    /// <summary>Clave de <c>c_ClaveUnidad</c>. Obligatoria en CFDI 4.0.</summary>
    public required string ClaveUnidad { get; set; }

    /// <summary>Unidad tal como la lee el receptor en el PDF.</summary>
    public required string UnidadTexto { get; set; }

    // «Nombre del producto» en pantalla; conserva este nombre porque va al CFDI como Descripcion.
    public required string Descripcion { get; set; }

    // «ID interno» en pantalla. No es único; precarga el NoIdentificacion (100 caracteres, sin «|»).
    public string? IdInterno { get; set; }

    // «Descripción» en pantalla.
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

    // Para filtrar por cuenta al consultar impuestos sin cargar el producto.
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
