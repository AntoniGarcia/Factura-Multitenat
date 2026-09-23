using Facturacion.Server.Data.Entidades.Plataforma;

namespace Facturacion.Server.Data.Entidades.Transporte;

/// <summary>
/// Operador u otra figura que interviene en una Carta Porte. Pertenece a una sola empresa.
/// </summary>
public sealed class FiguraTransporte : IEntidadDeEmpresa
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public required string Clave { get; set; }
    public required string TipoFigura { get; set; }
    public required string Rfc { get; set; }
    public required string Nombre { get; set; }
    public string? NumeroLicencia { get; set; }
    public required string Calle { get; set; }
    public required string NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public required string Estado { get; set; }
    public required string Municipio { get; set; }
    public required string CodigoPostal { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaAltaUtc { get; set; }
    public DateTime? FechaModificacionUtc { get; set; }
}
