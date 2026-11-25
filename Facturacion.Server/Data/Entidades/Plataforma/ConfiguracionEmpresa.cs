namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>Valores iniciales para capturar facturas. La configuración SMTP está separada para no incluir secretos junto con las tasas.</summary>
public sealed class ConfiguracionEmpresa : IEntidadDeEmpresa
{
    /// <summary>Es también la llave primaria: hay exactamente una configuración por empresa.</summary>
    public Guid EmpresaId { get; set; }

    public Empresa Empresa { get; set; } = null!;

    /// <summary>Tasa de IVA trasladado por omisión, como fracción: 0.160000 es 16 %.</summary>
    public decimal TasaIvaPorDefecto { get; set; }

    // Fracción; cero si la empresa no cobra IEPS.
    public decimal TasaIepsPorDefecto { get; set; }

    /// <summary>Retención de IVA por omisión, como fracción. Cero si la empresa no retiene.</summary>
    public decimal TasaRetencionIvaPorDefecto { get; set; }

    /// <summary>Retención de ISR por omisión, como fracción. Cero si la empresa no retiene.</summary>
    public decimal TasaRetencionIsrPorDefecto { get; set; }

    // Fracción; cero si la empresa no retiene IEPS.
    public decimal TasaRetencionIepsPorDefecto { get; set; }

    // Impuesto sobre hospedaje, como fracción: precarga la tasa de la factura básica.
    public decimal TasaIshPorDefecto { get; set; }

    /// <summary>Con cuántos días de anticipación avisar que el certificado va a caducar.</summary>
    public int DiasAvisoCaducidadCertificado { get; set; } = 30;
}
