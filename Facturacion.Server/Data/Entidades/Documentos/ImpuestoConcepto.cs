namespace Facturacion.Server.Data.Entidades.Documentos;

/// <summary>
/// Un impuesto de un renglón, ya calculado y congelado. Traslado o retención según
/// <see cref="EsRetencion"/>.
///
/// <para><b>Exento no es tasa cero</b></para>
/// Con <c>TipoFactor = "Exento"</c>, <see cref="TasaOCuota"/> e <see cref="Importe"/> van
/// nulos: el XML no lleva importe en ese nodo. Con tasa cero sí lo lleva, valiendo cero. Son
/// dos cosas distintas ante el SAT y confundirlas es rechazo del PAC.
/// </summary>
public sealed class ImpuestoConcepto : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid ConceptoId { get; set; }

    public Concepto Concepto { get; set; } = null!;

    /// <summary>Clave de <c>c_Impuesto</c>: 001 ISR, 002 IVA, 003 IEPS.</summary>
    public required string Impuesto { get; set; }

    /// <summary>Clave de <c>c_TipoFactor</c>: Tasa, Cuota o Exento.</summary>
    public required string TipoFactor { get; set; }

    /// <summary>Valor de <c>c_TasaOCuota</c>. Nulo solo cuando el tipo de factor es Exento.</summary>
    public decimal? TasaOCuota { get; set; }

    /// <summary>Base gravable del renglón: importe menos descuento.</summary>
    public decimal Base { get; set; }

    /// <summary>Base × tasa. Nulo cuando el tipo de factor es Exento.</summary>
    public decimal? Importe { get; set; }

    /// <summary>Verdadero si es retención; falso si es traslado.</summary>
    public bool EsRetencion { get; set; }
}
