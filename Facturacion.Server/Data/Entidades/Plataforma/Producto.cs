namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Producto o servicio del catálogo de la empresa. Es la plantilla de un concepto del CFDI:
/// al facturar se copian estos datos al comprobante y ahí se congelan (CLAUDE.md §5).
///
/// <para><b>Dos claves del SAT y una unidad en texto</b></para>
/// <see cref="ClaveProdServ"/> y <see cref="ClaveUnidad"/> son del catálogo y viajan al XML;
/// nunca se escriben a mano (CLAUDE.md §7). <see cref="UnidadTexto"/> es lo que el receptor
/// lee en el PDF —«Litro», «Caja con 12»— y no tiene que coincidir con la clave.
/// </summary>
public sealed class Producto : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Empresa Empresa { get; set; } = null!;

    /// <summary>Consecutivo por empresa, como la clave de cliente: los capturistas la usan para buscar.</summary>
    public int CodigoInterno { get; set; }

    /// <summary>Clave de <c>c_ClaveProdServ</c>. Obligatoria en CFDI 4.0.</summary>
    public required string ClaveProdServ { get; set; }

    /// <summary>Clave de <c>c_ClaveUnidad</c>. Obligatoria en CFDI 4.0.</summary>
    public required string ClaveUnidad { get; set; }

    /// <summary>Unidad tal como la lee el receptor en el PDF.</summary>
    public required string UnidadTexto { get; set; }

    public required string Descripcion { get; set; }

    /// <summary>Precio de venta. Seis decimales de cálculo, dos de presentación (CLAUDE.md §5).</summary>
    public decimal ValorUnitario { get; set; }

    /// <summary>Peso en kilogramos. Informativo; lo pide §5 del documento funcional.</summary>
    public decimal? PesoKg { get; set; }

    /// <summary>Clave de <c>c_ObjetoImp</c>. Decide si el concepto lleva desglose de impuestos.</summary>
    public required string ObjetoImp { get; set; }

    public ICollection<ProductoImpuesto> Impuestos { get; set; } = [];

    public bool Activo { get; set; } = true;

    public DateTime FechaAltaUtc { get; set; }

    public DateTime? FechaModificacionUtc { get; set; }
}

/// <summary>
/// Un impuesto configurado en un producto.
///
/// <para><b>Por qué no es «IVA 16 / IVA 0 / exento»</b></para>
/// El sistema viejo (§6 del documento funcional) usaba tres botones excluyentes. Eso no
/// alcanza para CFDI 4.0: un concepto puede llevar traslado de IVA y además retención de IVA
/// y de ISR a la vez, y el IEPS tiene tasas y hasta cuotas fijas. Se modelan las tres
/// columnas del SAT —impuesto, tipo de factor y tasa o cuota— y la interfaz ofrece los tres
/// casos comunes como atajos. Que la captura sea simple no obliga a que el modelo sea pobre.
/// </para>
/// </summary>
public sealed class ProductoImpuesto : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    /// <summary>
    /// Redundante con la del producto padre, y a propósito.
    ///
    /// <para>
    /// Sin ella esta tabla quedaba fuera del filtro global —lo advertía EF en cada arranque—
    /// y <c>AppDbContext.ProductosImpuestos</c> devolvía renglones de todas las empresas a
    /// quien lo consultara directo. Las lecturas de la mitad A entran por
    /// <c>Include(p =&gt; p.Impuestos)</c> desde <c>Productos</c>, que sí filtra, así que no
    /// hubo fuga; pero la puerta estaba abierta y la mitad B lee impuestos de producto para
    /// calcular comprobantes.
    /// </para>
    ///
    /// <para>
    /// Con la columna, el filtro global y el sellado del interceptor la cubren solos, como a
    /// cualquier otra tabla de empresa (CLAUDE.md §5). Se detectó en la verificación
    /// posterior a la fase 9; ver <c>docs/REPASO-SEGURIDAD.md</c>.
    /// </para>
    /// </summary>
    public Guid EmpresaId { get; set; }

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
