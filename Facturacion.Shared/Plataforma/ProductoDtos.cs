namespace Facturacion.Shared.Plataforma;

/// <summary>Un producto completo, para el formulario y el detalle.</summary>
public sealed record ProductoDto(
    Guid Id,
    int CodigoInterno,
    string ClaveProdServ,
    string ClaveUnidad,
    string UnidadTexto,
    string Descripcion,
    decimal ValorUnitario,
    decimal? PesoKg,
    string ObjetoImp,
    IReadOnlyList<ImpuestoDeProductoDto> Impuestos,
    bool Activo);

/// <summary>
/// Un impuesto del producto, con las tres columnas del SAT. Ver <c>ProductoImpuesto</c>
/// para por qué no se modela como «IVA 16 / IVA 0 / exento».
/// </summary>
public sealed record ImpuestoDeProductoDto(
    string Impuesto,
    string TipoFactor,
    decimal? TasaOCuota,
    bool EsRetencion);

/// <summary>Renglón de la lista, con lo que pide §5 del documento funcional.</summary>
public sealed record ProductoEnListaDto(
    Guid Id,
    int CodigoInterno,
    string Descripcion,
    string UnidadTexto,
    decimal ValorUnitario,
    decimal? PesoKg,
    string ClaveProdServ,
    bool Activo);

/// <summary>Alta y edición. El código interno lo asigna el servidor.</summary>
public sealed record PeticionGuardarProducto(
    string ClaveProdServ,
    string ClaveUnidad,
    string UnidadTexto,
    string Descripcion,
    decimal ValorUnitario,
    decimal? PesoKg,
    string ObjetoImp,
    IReadOnlyList<ImpuestoDeProductoDto> Impuestos,
    bool Activo);

/// <summary>Una página de productos con el total.</summary>
public sealed record PaginaDeProductos(
    IReadOnlyList<ProductoEnListaDto> Elementos,
    int Total);

/// <summary>
/// Un renglón del CSV ya analizado, listo para la vista previa. Trae el resultado de la
/// validación <b>antes</b> de confirmar: el contador ve qué va a entrar y qué no, y por qué.
/// </summary>
/// <param name="Numero">Renglón del archivo, empezando en 1 después del encabezado.</param>
/// <param name="Producto">Lo que se entendió del renglón. Nulo si ni siquiera se pudo leer.</param>
/// <param name="Error">Qué está mal. Nulo si el renglón es válido.</param>
public sealed record RenglonDeImportacion(
    int Numero,
    PeticionGuardarProducto? Producto,
    string? Error);

/// <summary>
/// Resultado de analizar un CSV. No guarda nada: es la vista previa que el usuario confirma.
/// </summary>
public sealed record VistaPreviaDeImportacion(
    IReadOnlyList<RenglonDeImportacion> Renglones,
    int Validos,
    int ConError);

/// <summary>Lo que se guardó de verdad al confirmar una importación.</summary>
public sealed record ResultadoDeImportacion(
    int Creados,
    int Actualizados,
    IReadOnlyList<RenglonDeImportacion> Rechazados);
