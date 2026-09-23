namespace Facturacion.Shared.Transporte;

public sealed record VehiculoDto(
    Guid Id, string Clave, string Descripcion, string ConfiguracionAutotransporte, string Placa,
    int AnioModelo, string Aseguradora, string Poliza, string TipoPermiso, string NumeroPermiso,
    decimal PesoBrutoVehicular, bool Activo);

public sealed record PeticionGuardarVehiculo(
    string Clave, string Descripcion, string ConfiguracionAutotransporte, string Placa,
    int AnioModelo, string Aseguradora, string Poliza, string TipoPermiso, string NumeroPermiso,
    decimal PesoBrutoVehicular, bool Activo);

public sealed record FiguraTransporteDto(
    Guid Id, string Clave, string TipoFigura, string Rfc, string Nombre, string? NumeroLicencia,
    string Calle, string NumeroExterior, string? NumeroInterior, string Estado, string Municipio,
    string CodigoPostal, bool Activo);

public sealed record PeticionGuardarFiguraTransporte(
    string Clave, string TipoFigura, string Rfc, string Nombre, string? NumeroLicencia,
    string Calle, string NumeroExterior, string? NumeroInterior, string Estado, string Municipio,
    string CodigoPostal, bool Activo);

/// <summary>Estado y municipio oficiales asociados a un código postal del SAT.</summary>
public sealed record DomicilioPorCodigoPostalDto(
    string CodigoPostal,
    string Estado,
    string NombreEstado,
    string? Municipio,
    string? NombreMunicipio);

/// <summary>
/// Borrador de un traslado de mercancía propia por autotransporte. La empresa activa no
/// viaja en esta petición: el servidor la determina desde el token.
/// </summary>
public sealed record PeticionGuardarTrasladoCartaPorte(
    Guid VehiculoId,
    Guid FiguraTransporteId,
    DateTime FechaSalidaLocal,
    DateTime FechaLlegadaLocal,
    decimal DistanciaRecorridaKm,
    IReadOnlyList<UbicacionCartaPorteDto> Ubicaciones,
    IReadOnlyList<MercanciaCartaPorteDto> Mercancias);

public sealed record UbicacionCartaPorteDto(
    string Tipo,
    int Orden,
    string? RfcRemitenteDestinatario,
    string Calle,
    string NumeroExterior,
    string? NumeroInterior,
    string Estado,
    string Municipio,
    string CodigoPostal);

public sealed record MercanciaCartaPorteDto(
    int Orden,
    string ClaveProdServ,
    string Descripcion,
    decimal Cantidad,
    string ClaveUnidad,
    decimal PesoEnKg);

/// <summary>Resultado de validar el XML de un traslado sin enviarlo a un PAC.</summary>
public sealed record ValidacionXmlCartaPorteDto(string Mensaje);

/// <summary>Datos persistidos de un traslado propio. Los horarios se devuelven en UTC.</summary>
public sealed record TrasladoCartaPorteDto(
    Guid ComprobanteId,
    string Estatus,
    Guid VehiculoId,
    Guid FiguraTransporteId,
    DateTime FechaSalidaUtc,
    DateTime FechaLlegadaUtc,
    decimal DistanciaRecorridaKm,
    decimal PesoBrutoTotalKg,
    decimal TotalMercancias,
    IReadOnlyList<UbicacionCartaPorteDto> Ubicaciones,
    IReadOnlyList<MercanciaCartaPorteDto> Mercancias);
