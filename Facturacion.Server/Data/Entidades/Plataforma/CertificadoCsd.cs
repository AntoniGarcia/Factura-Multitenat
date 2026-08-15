namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Certificado de sello digital de la empresa. Es el material más sensible del sistema:
/// con él se firman comprobantes fiscales a nombre del contribuyente.
///
/// <para><b>Qué guarda esta tabla y qué no</b></para>
/// Aquí solo viven los <b>metadatos</b> —número de serie, vigencia, dónde está el archivo— y
/// la contraseña <b>cifrada</b>. Los archivos <c>.cer</c> y <c>.key</c> viven cifrados en el
/// almacén, fuera de <c>wwwroot</c> (CLAUDE.md §4). Nada de esto vuelve nunca al
/// <c>Client</c>: la pantalla muestra número de serie y vigencia, y nada más.
///
/// <para><b>Por qué el anterior no se borra</b></para>
/// Un comprobante timbrado hace dos años lleva dentro el número de serie del certificado con
/// el que se selló. Si se borrara el renglón, no habría forma de explicar de dónde salió ese
/// número. Se conserva con <see cref="Activo"/> en falso, como todo en este sistema
/// (CLAUDE.md §5).
/// </summary>
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
