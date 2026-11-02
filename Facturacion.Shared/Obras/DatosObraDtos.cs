namespace Facturacion.Shared.Obras;

public static class TiposDeObra
{
    public const string Publica = "publica";
    public const string Privada = "privada";
}

public sealed record DeduccionDeObraDto(string Nombre, decimal Porcentaje, decimal Importe);
public sealed record PeticionDeduccionDeObra(string Nombre, decimal Porcentaje);
public sealed record ConciliacionFiscalObraDto(bool Coincide, string Mensaje);

public sealed record PeticionGuardarDatosObra(
    decimal PorcentajeAmortizacion,
    decimal PorcentajeRetenciones,
    decimal PorcentajeDevoluciones,
    IReadOnlyList<PeticionDeduccionDeObra> Deducciones,
    string? TipoObra = null);

/// <param name="IvaEstimado">IVA trasladado de los conceptos, tal como sale en el CFDI.</param>
public sealed record DatosObraDto(
    decimal ImporteTrabajos,
    decimal PorcentajeAmortizacion,
    decimal Amortizacion,
    decimal? PorcentajeRetenciones,
    decimal Retenciones,
    decimal? PorcentajeDevoluciones,
    decimal Devoluciones,
    decimal SubtotalEstimacion,
    decimal IvaEstimado,
    decimal TotalEstimacion,
    IReadOnlyList<DeduccionDeObraDto> Deducciones,
    decimal ImporteLiquido,
    string? TipoObra = null);
