using Facturacion.Shared.Comun;

namespace Facturacion.Shared.Contratos;

/// <summary>Persistencia fiscal del proveedor, exclusiva de la emisión de compras desde el servidor.</summary>
public interface IEmisionDeFacturasDeCompras
{
    /// <summary>Congela un intento y reserva folio sin tocar la bolsa del comprador. La clave repetida recupera el mismo intento.</summary>
    Task<Resultado<IntentoFacturaCompraDto>> PrepararAsync(
        Guid compraId, byte[] version, string claveIdempotencia, CancellationToken ct);

    /// <summary>Obtiene el CSD fijado al preparar el intento y registra su acceso. Nunca se serializa.</summary>
    Task<Resultado<CsdDescifradoDto>> ObtenerCsdAsync(Guid intentoId, Guid control, CancellationToken ct);

    /// <summary>Persiste el XML exacto antes de enviarlo. Una segunda escritura no puede cambiarlo.</summary>
    Task<Resultado> GuardarXmlAsync(Guid intentoId, Guid control, XmlFacturaProveedorDto xml, CancellationToken ct);

    /// <summary>Guarda un timbrado o un fallo definitivo. Un resultado incierto no se resuelve por este método.</summary>
    Task<Resultado> ResolverAsync(
        Guid intentoId, Guid control, ResolucionFacturaProveedorDto resolucion, CancellationToken ct);

    /// <summary>Reclama hasta cien intentos antiguos para conciliarlos. Renueva su control y excluye solicitudes recientes.</summary>
    Task<IReadOnlyList<IntentoFacturaCompraDto>> ObtenerPendientesAsync(
        DateTime limiteUtc, int maximo, CancellationToken ct);
}

public sealed record EmisorFacturaProveedorDto(
    string Rfc, string Nombre, string Regimen, string CodigoPostal, string ZonaHoraria, string Serie);

public sealed record ReceptorFacturaProveedorDto(
    string Rfc, string Nombre, string Regimen, string CodigoPostal, string UsoCfdi);

public sealed record DatosFacturaProveedorDto(
    EmisorFacturaProveedorDto Emisor, ReceptorFacturaProveedorDto Receptor,
    string Descripcion, string ClaveProdServ, string ClaveUnidad, string FormaPago,
    decimal Subtotal, decimal Iva, decimal TasaIva, decimal Total);

/// <summary>Control cambia al reclamar un intento; una ejecución anterior ya no puede modificarlo.</summary>
public sealed record IntentoFacturaCompraDto(
    Guid Id, Guid Control, Guid CompraId, bool IniciadoAhora, string Estado,
    string ClavePac, int Folio, DateTime FechaEmisionUtc,
    DatosFacturaProveedorDto Datos, XmlFacturaProveedorDto? Xml);

public sealed record XmlFacturaProveedorDto(
    string Xml, string CadenaOriginal, string Sello, string NoCertificado);

public enum ResultadoFacturaProveedor
{
    Timbrado,
    Rechazado,
    ErrorDePreparacion
}

public sealed record ResolucionFacturaProveedorDto(
    ResultadoFacturaProveedor Resultado, string? CodigoError = null,
    Guid? Uuid = null, DateTime? FechaTimbradoUtc = null, string? XmlTimbrado = null,
    string? NoCertificadoSat = null, string? SelloSat = null, string? CadenaOriginalSat = null);
