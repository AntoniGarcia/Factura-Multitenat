namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Valores por omisión de la empresa (§3 del documento funcional, solo lo que aplica).
/// Alimentan los cálculos de los formularios de facturación; no son la verdad fiscal, son
/// el punto de partida para capturar más rápido.
/// <para>
/// La configuración SMTP del cliente <b>no está aquí y no va a estar</b>: se descartó por
/// arquitectura (REPARTO-EQUIPO.md §1), porque custodiar contraseñas de correo reutilizables
/// de decenas de empresas no vale el riesgo. Todo sale de un remitente propio del SaaS.
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

    /// <summary>Retención de IVA por omisión, como fracción. Cero si la empresa no retiene.</summary>
    public decimal TasaRetencionIvaPorDefecto { get; set; }

    /// <summary>Retención de ISR por omisión, como fracción. Cero si la empresa no retiene.</summary>
    public decimal TasaRetencionIsrPorDefecto { get; set; }

    /// <summary>Con cuántos días de anticipación avisar que el certificado va a caducar.</summary>
    public int DiasAvisoCaducidadCertificado { get; set; } = 30;
}
