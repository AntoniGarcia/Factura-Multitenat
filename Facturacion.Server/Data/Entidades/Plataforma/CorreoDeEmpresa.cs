namespace Facturacion.Server.Data.Entidades.Plataforma;

// Único camino de los comprobantes de la empresa. El puerto decide el TLS: 465 directo o 587
// con STARTTLS.
public sealed class CorreoDeEmpresa : IEntidadDeEmpresa
{
    // También es la llave primaria: un servidor de correo por empresa.
    public Guid EmpresaId { get; set; }

    public Empresa Empresa { get; set; } = null!;

    public string? Servidor { get; set; }

    public int Puerto { get; set; } = 587;

    public string? Usuario { get; set; }

    // Cifrada con un propósito propio por empresa; nunca sale del servidor.
    public string? ContrasenaCifrada { get; set; }

    public string? RemitenteNombre { get; set; }

    public string? RemitenteCorreo { get; set; }
}
