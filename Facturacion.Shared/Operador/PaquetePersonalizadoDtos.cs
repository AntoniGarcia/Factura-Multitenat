using System.ComponentModel.DataAnnotations;

namespace Facturacion.Shared.Operador;

public sealed record PaquetePersonalizadoDto(
    Guid Id,
    string Nombre,
    int CantidadTimbres,
    decimal PrecioPorTimbre,
    decimal PrecioTotal,
    int VigenciaMeses,
    bool Activo,
    int Compras);

/// <summary>El precio total ya incluye IVA; el servidor calcula el unitario y el desglose.</summary>
public sealed record PeticionGuardarPaquetePersonalizado(
    [property: Required, MaxLength(100)] string Nombre,
    [property: Range(1, 1_000_000)] int CantidadTimbres,
    [property: Range(0.01, 10_000_000)] decimal PrecioTotal,
    [property: Range(1, 24)] int VigenciaMeses);

public sealed record PeticionCambiarActivoPaquete(bool Activo);
