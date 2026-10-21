namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>Valores iniciales para capturar facturas. La configuración SMTP está separada para no incluir secretos junto con las tasas.</summary>
public sealed class ConfiguracionEmpresa : IEntidadDeEmpresa
{
    /// <summary>Es también la llave primaria: hay exactamente una configuración por empresa.</summary>
    public Guid EmpresaId { get; set; }

    public Empresa Empresa { get; set; } = null!;

    /// <summary>Tasa de IVA trasladado por omisión, como fracción: 0.160000 es 16 %.</summary>
    public decimal TasaIvaPorDefecto { get; set; }

    /// <summary>Tasa de IEPS trasladado por omisión, como fracción. Cero si la empresa no lo cobra.</summary>
    public decimal TasaIepsPorDefecto { get; set; }

    /// <summary>Retención de IVA por omisión, como fracción. Cero si la empresa no retiene.</summary>
    public decimal TasaRetencionIvaPorDefecto { get; set; }

    /// <summary>Retención de ISR por omisión, como fracción. Cero si la empresa no retiene.</summary>
    public decimal TasaRetencionIsrPorDefecto { get; set; }

    /// <summary>Retención de IEPS por omisión, como fracción. Cero si la empresa no retiene.</summary>
    public decimal TasaRetencionIepsPorDefecto { get; set; }

    /// <summary>
    /// Impuesto sobre hospedaje por omisión, como fracción. Es local: solo puede viajar en el
    /// complemento de impuestos locales, y hoy ningún comprobante lo aplica. Se guarda para el
    /// documento que lo necesite.
    /// </summary>
    public decimal TasaIshPorDefecto { get; set; }

    /// <summary>Con cuántos días de anticipación avisar que el certificado va a caducar.</summary>
    public int DiasAvisoCaducidadCertificado { get; set; } = 30;
}
