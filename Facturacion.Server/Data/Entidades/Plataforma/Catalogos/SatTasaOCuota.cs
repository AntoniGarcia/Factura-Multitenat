namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>
/// Catálogo <c>c_TasaOCuota</c>: las tasas de IVA (16 %, 8 %, 0 %), la cuota fija de IEPS por
/// producto, y las retenciones de ISR e IVA. El SAT no le da una clave propia por renglón
/// —lo que identifica un renglón es la combinación impuesto+factor+valor—, así que
/// <see cref="Clave"/> se construye al importar y no viene del archivo.
/// </summary>
public sealed class SatTasaOCuota
{
    /// <summary>Construida como <c>Impuesto|Factor|ValorMaximo</c> al importar.</summary>
    public required string Clave { get; set; }

    /// <summary>"Rango" cuando el impuesto varía por tramos, "Fijo" cuando es un único valor.</summary>
    public required string RangoOFijo { get; set; }

    public decimal? ValorMinimo { get; set; }

    /// <summary>El valor de la tasa (0.16) o de la cuota fija, según <see cref="Factor"/>.</summary>
    public decimal ValorMaximo { get; set; }

    /// <summary>Clave de <see cref="SatImpuesto"/> tal como la escribe el SAT aquí (por ejemplo "IVA").</summary>
    public required string Impuesto { get; set; }

    /// <summary>"Tasa", "Cuota" o "Exento".</summary>
    public required string Factor { get; set; }

    public bool Traslado { get; set; }
    public bool Retencion { get; set; }
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}
