namespace Facturacion.Shared.Contratos;

/// <summary>
/// Consulta de los catálogos del SAT. Los catálogos grandes nunca se devuelven completos:
/// se resuelven por clave exacta o por búsqueda con tope (CLAUDE.md §7).
/// </summary>
public interface IServicioCatalogosSat
{
    /// <summary>
    /// Resuelve una clave exacta dentro de un catálogo. Devuelve <c>null</c> si no existe.
    /// Una clave retirada por el SAT sigue resolviendo, con <see cref="ClaveSatDto.Vigente"/>
    /// en falso: un comprobante viejo tiene que poder seguir mostrando su descripción.
    /// </summary>
    /// <param name="catalogo">Nombre del catálogo, por ejemplo <c>c_ClaveProdServ</c>.</param>
    Task<ClaveSatDto?> ResolverAsync(string catalogo, string clave, CancellationToken ct);

    /// <summary>
    /// Búsqueda por texto. Exige al menos tres caracteres y respeta el tope pedido.
    /// </summary>
    Task<IReadOnlyList<ClaveSatDto>> BuscarAsync(string catalogo, string texto, int tope, CancellationToken ct);

    /// <summary>
    /// Valida la matriz de compatibilidad de <c>c_UsoCFDI</c>: qué usos admite cada régimen
    /// fiscal y cada tipo de persona. Es la validación que más timbrados evita rechazar.
    /// </summary>
    Task<bool> EsUsoCfdiCompatibleAsync(string usoCfdi, string regimenReceptor, bool esPersonaMoral, CancellationToken ct);
}

/// <summary>Una entrada de un catálogo del SAT.</summary>
/// <param name="Catalogo">Catálogo del que proviene, por ejemplo <c>c_ClaveUnidad</c>.</param>
/// <param name="Clave">Clave tal como viaja al CFDI.</param>
/// <param name="Descripcion">Descripción publicada por el SAT.</param>
/// <param name="Vigente">Falso si el SAT la retiró; se conserva para poder leer comprobantes viejos.</param>
public sealed record ClaveSatDto(
    string Catalogo,
    string Clave,
    string Descripcion,
    bool Vigente);
