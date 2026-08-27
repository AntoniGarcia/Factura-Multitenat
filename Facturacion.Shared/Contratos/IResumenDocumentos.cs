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

    /// <summary>
    /// Comprobantes timbrados e importe de cada uno de los ultimos <paramref name="meses"/>
    /// meses naturales, del mas antiguo al mas reciente y con los meses sin actividad en
    /// cero. Alimenta las graficas del tablero.
    /// <para>
    /// Se anadio despues de congelar el contrato, y es una ampliacion: ningun miembro
    /// existente cambio de forma, asi que nada de lo que ya consumia la mitad A se rompe.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<PuntoMensualDeDocumentosDto>> SerieMensualAsync(int meses, CancellationToken ct);
}

/// <summary>Un mes de la serie del tablero.</summary>
/// <param name="Etiqueta">Mes ya rotulado para pintar, en el huso de la empresa.</param>
/// <param name="Timbrados">Cuantos comprobantes se timbraron ese mes.</param>
/// <param name="Importe">Suma de los totales timbrados de ese mes.</param>
public sealed record PuntoMensualDeDocumentosDto(
    string Etiqueta,
    int Timbrados,
    decimal Importe);

/// <summary>Cifras del tablero para un periodo.</summary>
/// <param name="ConteoPorEstatus">Comprobantes por estatus. Un estatus sin comprobantes puede venir ausente o en cero.</param>
/// <param name="ImporteTimbrado">Suma de los totales de los comprobantes timbrados del periodo.</param>
/// <param name="ImporteCancelado">Suma de los totales de los comprobantes cancelados del periodo.</param>
public sealed record ResumenDocumentosDto(
    IReadOnlyDictionary<EstatusComprobante, int> ConteoPorEstatus,
    decimal ImporteTimbrado,
    decimal ImporteCancelado);
