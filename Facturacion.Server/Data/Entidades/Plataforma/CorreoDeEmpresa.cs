namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// SMTP de la empresa para sus comprobantes, separado de la configuración fiscal. Es el único
/// camino: sin él completo los comprobantes no se envían, y el SMTP del sistema queda solo para
/// los correos de la plataforma. El puerto determina TLS: 465 directo o 587 con STARTTLS.
/// </summary>
public sealed class CorreoDeEmpresa : IEntidadDeEmpresa
{
    /// <summary>Es también la llave primaria: un servidor de correo por empresa.</summary>
    public Guid EmpresaId { get; set; }

    public Empresa Empresa { get; set; } = null!;

    public string? Servidor { get; set; }

    public int Puerto { get; set; } = 587;

    public string? Usuario { get; set; }

    /// <summary>Cifrada con un propósito propio por empresa. Nunca sale del servidor.</summary>
    public string? ContrasenaCifrada { get; set; }

    public string? RemitenteNombre { get; set; }

    public string? RemitenteCorreo { get; set; }
}
