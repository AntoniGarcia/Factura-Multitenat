namespace Facturacion.Shared.Notaria;

/// <summary>Configuración reutilizable que identifica al notario en el complemento fiscal.</summary>
public sealed record ConfiguracionNotarioDto(string Curp, int NumeroNotaria, string Estado, string? Adscripcion);

/// <summary>Datos que captura la empresa con licencia de Notaría.</summary>
public sealed record PeticionGuardarConfiguracionNotario(
    string Curp,
    int NumeroNotaria,
    string Estado,
    string? Adscripcion);

/// <summary>
/// Datos de la operación y de los inmuebles de un comprobante notarial. La fecha es civil,
/// no una hora de evento: el XSD del SAT la define como <c>xs:date</c>.
/// </summary>
public sealed record DatosNotariaDto(
    Guid ComprobanteId,
    int NumeroInstrumentoNotarial,
    DateOnly FechaInstrumentoNotarial,
    decimal MontoOperacion,
    decimal SubtotalOperacion,
    decimal IvaOperacion,
    IReadOnlyList<InmuebleNotarialDto> Inmuebles);

/// <summary>Un inmueble o servidumbre de paso incluido en una operación notarial.</summary>
public sealed record InmuebleNotarialDto(
    Guid Id,
    int Orden,
    string TipoInmueble,
    string Calle,
    string? NumeroExterior,
    string? NumeroInterior,
    string? Colonia,
    string? Localidad,
    string? Referencia,
    string Municipio,
    string Estado,
    string Pais,
    string CodigoPostal);

/// <summary>Captura editable de la operación notarial de un borrador.</summary>
public sealed record PeticionGuardarDatosNotaria(
    int NumeroInstrumentoNotarial,
    DateOnly FechaInstrumentoNotarial,
    decimal MontoOperacion,
    decimal SubtotalOperacion,
    decimal IvaOperacion,
    IReadOnlyList<InmuebleNotarialDto> Inmuebles);

/// <summary>Vendedores y compradores que exige el complemento por cada operación.</summary>
public sealed record PartesNotarialesDto(
    bool EnajenantesEnCopropiedad,
    IReadOnlyList<ParteNotarialDto> Enajenantes,
    bool AdquirentesEnCopropiedad,
    IReadOnlyList<ParteNotarialDto> Adquirentes);

/// <summary>Una persona física o moral que enajena o adquiere dentro del instrumento.</summary>
public sealed record ParteNotarialDto(
    Guid Id,
    int Orden,
    string Nombre,
    string? ApellidoPaterno,
    string? ApellidoMaterno,
    string Rfc,
    string? Curp,
    decimal? Porcentaje);

/// <summary>Guarda ambos lados de la operación; el servidor valida su modalidad y porcentajes.</summary>
public sealed record PeticionGuardarPartesNotariales(
    bool EnajenantesEnCopropiedad,
    IReadOnlyList<ParteNotarialDto> Enajenantes,
    bool AdquirentesEnCopropiedad,
    IReadOnlyList<ParteNotarialDto> Adquirentes);
