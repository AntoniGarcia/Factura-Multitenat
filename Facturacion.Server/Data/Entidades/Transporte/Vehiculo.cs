using Facturacion.Server.Data.Entidades.Plataforma;

namespace Facturacion.Server.Data.Entidades.Transporte;

/// <summary>
/// Vehículo reutilizable de la empresa para un traslado con Carta Porte. Los valores que
/// exige el SAT se guardan aquí para no volver a capturarlos en cada documento.
/// </summary>
public sealed class Vehiculo : IEntidadDeEmpresa
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public required string Clave { get; set; }
    public required string Descripcion { get; set; }
    public required string ConfiguracionAutotransporte { get; set; }
    public required string Placa { get; set; }
    public int AnioModelo { get; set; }
    public required string Aseguradora { get; set; }
    public required string Poliza { get; set; }
    public required string TipoPermiso { get; set; }
    public required string NumeroPermiso { get; set; }
    public decimal PesoBrutoVehicular { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaAltaUtc { get; set; }
    public DateTime? FechaModificacionUtc { get; set; }
}
