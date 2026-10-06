namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Servidor SMTP propio de la empresa, para enviar sus comprobantes desde su dominio
/// (AGENTS.md §11, 5 de octubre de 2026).
/// <para>
/// Va aparte de <see cref="ConfiguracionEmpresa"/> para que el secreto no viaje con las tasas
/// por omisión y la bitácora de una no arrastre a la otra. Sin renglón, o con
/// <see cref="Habilitado"/> en falso, los comprobantes salen del SMTP del SaaS como siempre.
/// </para>
/// <para>
/// No hay campo de seguridad: el puerto la decide. 465 es SSL directo y 587 es STARTTLS; no
/// se admite ningún modo sin cifrar, porque mandaría la contraseña en claro.
/// </para>
/// </summary>
public sealed class CorreoDeEmpresa : IEntidadDeEmpresa
{
    /// <summary>Es también la llave primaria: un servidor de correo por empresa.</summary>
    public Guid EmpresaId { get; set; }

    public Empresa Empresa { get; set; } = null!;

    public bool Habilitado { get; set; }

    public string? Servidor { get; set; }

    public int Puerto { get; set; } = 587;

    public string? Usuario { get; set; }

    /// <summary>Cifrada con un propósito propio por empresa. Nunca sale del servidor.</summary>
    public string? ContrasenaCifrada { get; set; }

    public string? RemitenteNombre { get; set; }

    public string? RemitenteCorreo { get; set; }

    // ── Cuando sale del remitente del sistema (Habilitado en falso) ─────────────────────

    /// <summary>
    /// Nombre que acompaña a la dirección del sistema. Nulo: el del sistema. No admite «@»,
    /// para que nadie se presente como otra dirección dentro del nombre.
    /// </summary>
    public string? NombreRemitenteSistema { get; set; }

    /// <summary>A dónde llegan las respuestas. Nulo: al correo de contacto de la empresa.</summary>
    public string? ResponderA { get; set; }
}
