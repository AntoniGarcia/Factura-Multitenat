using Microsoft.AspNetCore.Components;

namespace Facturacion.Client.Componentes.Comunes;

/// <summary>Una columna de <see cref="TablaDatos{T}"/>.</summary>
/// <param name="Encabezado">Texto del encabezado, en sentence case (ARQUITECTURA.md §8).</param>
/// <param name="Contenido">Cómo pintar la celda para un renglón.</param>
/// <param name="ClaveOrden">
/// Clave que el servidor entiende para ordenar por esta columna. Nula si la columna no se
/// puede ordenar.
/// </param>
/// <param name="Tabular">
/// Verdadero si el contenido es RFC, UUID, folio o importe: se pinta con
/// <c>--tipografia-monoespaciada</c> y cifras tabulares (ARQUITECTURA.md §8).
/// </param>
public sealed record ColumnaDeTabla<T>(
    string Encabezado,
    RenderFragment<T> Contenido,
    string? ClaveOrden = null,
    bool Tabular = false);

/// <summary>Lo que <see cref="TablaDatos{T}"/> le pide al servidor para traer una página.</summary>
public sealed record PeticionDePagina(
    int Pagina,
    int TamanoPagina,
    string? ClaveOrden,
    bool Descendente);

/// <summary>Lo que el servidor devuelve: la página pedida y el total de renglones que existen.</summary>
public sealed record ResultadoDePagina<T>(IReadOnlyList<T> Elementos, int Total)
{
    public static ResultadoDePagina<T> Vacio { get; } = new([], 0);
}
