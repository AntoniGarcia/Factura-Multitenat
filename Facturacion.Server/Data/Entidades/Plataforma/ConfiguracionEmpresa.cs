namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Valores por omisión de la empresa (§3 del documento funcional, solo lo que aplica).
/// Alimentan los cálculos de los formularios de facturación; no son la verdad fiscal, son
/// el punto de partida para capturar más rápido.
/// <para>
/// El servidor SMTP propio de la empresa no está aquí sino en <see cref="CorreoDeEmpresa"/>:
/// así su contraseña no viaja junto con las tasas (AGENTS.md §11).
/// </para>
/// <para>
/// Es una entidad aparte de <see cref="Empresa"/> y no unas columnas más, porque la
/// configuración la toca el permiso <c>configurar_empresa</c> con frecuencia mientras que
/// los datos fiscales casi nunca cambian; separarlas deja la bitácora legible.
/// </para>
/// </summary>
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
