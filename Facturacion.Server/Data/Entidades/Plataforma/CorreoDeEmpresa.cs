namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>SMTP propio para comprobantes, separado de la configuración fiscal. Sin habilitarlo se usa el del sistema. El puerto determina TLS: 465 directo o 587 con STARTTLS.</summary>
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
