namespace Facturacion.Shared.ComercioExterior;

public sealed record DomicilioComercioExteriorDto(
    string? Calle, string? NumeroExterior, string? NumeroInterior, string? Colonia,
    string? Localidad, string? Referencia, string? Municipio, string? Estado,
    string? Pais, string? CodigoPostal);

public sealed record DatosComercioExteriorDto(
    string? ResidenciaFiscal, string? NumeroRegistroTributario,
    string? ClavePedimento, bool CertificadoOrigen, string? NumeroCertificadoOrigen,
    string? NumeroExportadorConfiable, string? Observaciones,
    decimal? TipoCambioUsd, decimal? TotalUsd, string? CurpEmisor,
    DomicilioComercioExteriorDto DomicilioEmisor,
    DomicilioComercioExteriorDto DomicilioReceptor,
    string? Incoterm = null,
    IReadOnlyList<MercanciaComercioExteriorDto>? Mercancias = null);

public sealed record MercanciaComercioExteriorDto(
    int OrdenConcepto, string ClaveProdServConcepto, string DescripcionConcepto,
    string? FraccionArancelaria, string? UnidadAduana,
    decimal? CantidadAduana, decimal? ValorUnitarioAduana, decimal? ValorDolares,
    string? Marca, string? Modelo, string? Submodelo, string? NumeroSerie);
