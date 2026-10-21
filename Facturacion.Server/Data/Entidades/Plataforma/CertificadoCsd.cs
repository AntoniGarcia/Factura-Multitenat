namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>Metadatos y contraseña cifrada del CSD. Los archivos permanecen cifrados fuera de wwwroot y los certificados anteriores se conservan para trazabilidad.</summary>
public sealed class CertificadoCsd : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Empresa Empresa { get; set; } = null!;

    /// <summary>
    /// Número de serie tal como viaja en el atributo <c>NoCertificado</c> del XML: veinte
    /// dígitos. No es el <c>SerialNumber</c> hexadecimal que expone .NET —ver
    /// <c>LectorDeCsd</c> para la conversión.
    /// </summary>
    public required string NumeroSerie { get; set; }

    public DateTime VigenciaDesdeUtc { get; set; }

    public DateTime VigenciaHastaUtc { get; set; }

    /// <summary>Ruta del <c>.cer</c> dentro del almacén cifrado.</summary>
    public required string RutaCer { get; set; }

    /// <summary>Ruta del <c>.key</c> dentro del almacén cifrado.</summary>
    public required string RutaKey { get; set; }

    /// <summary>
    /// Contraseña de la llave privada, cifrada con Data Protection y atada a la empresa.
    /// Nunca se registra en el log, nunca se serializa y nunca vuelve al <c>Client</c>.
    /// </summary>
    public required string ContrasenaCifrada { get; set; }

    /// <summary>Solo uno por empresa puede estar activo; al cargar uno nuevo, el anterior se desactiva.</summary>
    public bool Activo { get; set; } = true;

    public DateTime FechaCargaUtc { get; set; }

    public Guid CargadoPorUsuarioId { get; set; }
}
