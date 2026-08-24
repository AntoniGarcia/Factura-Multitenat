namespace Facturacion.Shared.Contratos;

/// <summary>
/// Datos de la empresa emisora activa. Deliberadamente no expone el CSD: la llave privada
/// vive en <see cref="IProveedorCsdParaTimbrado"/>, que casi nadie debe resolver.
/// </summary>
public interface IServicioEmpresaEmisora
{
    /// <summary>Datos fiscales del emisor, listos para congelarse en el comprobante.</summary>
    Task<EmisorFiscalDto> ObtenerParaTimbradoAsync(CancellationToken ct);

    /// <summary>
    /// Logo de la empresa para el PDF. Devuelve <c>null</c> si no se ha cargado ninguno.
    /// Los archivos viven fuera de <c>wwwroot</c>; este método es la única vía por la que
    /// la mitad B los obtiene.
    /// </summary>
    Task<ArchivoDto?> ObtenerLogoAsync(CancellationToken ct);
}

/// <summary>
/// Datos fiscales del emisor tal como van a viajar al CFDI 4.0.
/// </summary>
/// <param name="EmpresaId">Empresa activa.</param>
/// <param name="Rfc">RFC del emisor.</param>
/// <param name="Nombre">Razón social normalizada.</param>
/// <param name="RegimenFiscal">Clave de <c>c_RegimenFiscal</c> del emisor.</param>
/// <param name="CodigoPostalExpedicion">Código postal del lugar de expedición.</param>
/// <param name="ZonaHoraria">
/// Huso del lugar de expedición. La base guarda todo en UTC; el sello del comprobante
/// tiene que llevar la hora local del lugar de expedición (ARQUITECTURA.md §5).
/// </param>
public sealed record EmisorFiscalDto(
    Guid EmpresaId,
    string Rfc,
    string Nombre,
    string RegimenFiscal,
    string CodigoPostalExpedicion,
    string ZonaHoraria);

/// <summary>Un archivo entregado desde el almacén de la plataforma.</summary>
/// <param name="Contenido">Bytes del archivo.</param>
/// <param name="TipoMime">Tipo de contenido verificado al cargarlo, no deducido de la extensión.</param>
/// <param name="NombreOriginal">Nombre con el que se cargó, solo para mostrar.</param>
public sealed record ArchivoDto(
    byte[] Contenido,
    string TipoMime,
    string NombreOriginal);
