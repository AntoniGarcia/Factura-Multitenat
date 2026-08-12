using Facturacion.Shared.Comun;

namespace Facturacion.Shared.Contratos;

/// <summary>
/// Lo único que la mitad A necesita de la mitad B: el resumen del tablero. Solo lectura.
/// </summary>
public interface IResumenDocumentos
{
    /// <summary>
    /// Resumen de los comprobantes de la empresa activa en el periodo, ambos extremos incluidos.
    /// </summary>
    Task<ResumenDocumentosDto> ObtenerAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
}

/// <summary>Cifras del tablero para un periodo.</summary>
/// <param name="ConteoPorEstatus">Comprobantes por estatus. Un estatus sin comprobantes puede venir ausente o en cero.</param>
/// <param name="ImporteTimbrado">Suma de los totales de los comprobantes timbrados del periodo.</param>
/// <param name="ImporteCancelado">Suma de los totales de los comprobantes cancelados del periodo.</param>
public sealed record ResumenDocumentosDto(
    IReadOnlyDictionary<EstatusComprobante, int> ConteoPorEstatus,
    decimal ImporteTimbrado,
    decimal ImporteCancelado);
