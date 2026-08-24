namespace Facturacion.Shared.Comun;

/// <summary>
/// Los tres temas del sistema (ARQUITECTURA.md §8). Las claves son las que viajan al servidor y
/// las que el Client escribe en el atributo <c>data-tema</c> del elemento raíz.
/// </summary>
public static class Temas
{
    public const string Claro = "claro";
    public const string Oscuro = "oscuro";
    public const string Calido = "calido";

    public static readonly IReadOnlyList<string> Todos = [Claro, Oscuro, Calido];

    public static bool EsValido(string? tema) => tema is not null && Todos.Contains(tema);
}
