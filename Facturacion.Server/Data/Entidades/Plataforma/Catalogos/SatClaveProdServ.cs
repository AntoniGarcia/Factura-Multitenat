namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>
/// Catálogo <c>c_ClaveProdServ</c> (~52,000 claves). No viaja completo al Client en ningún
/// caso (ARQUITECTURA.md §7); se resuelve por clave exacta o se busca por texto completo sobre
/// <see cref="Descripcion"/>.
/// </summary>
public sealed class SatClaveProdServ : ISatCatalogoSimple
{
    /// <summary>Ocho dígitos. El SAT los escribe con ceros a la izquierda cuando hace falta.</summary>
    public required string Clave { get; set; }

    public required string Descripcion { get; set; }

    /// <summary>"Sí", "No" u "Opcional", tal como los publica el SAT: no es un booleano.</summary>
    public string? IncluirIvaTrasladado { get; set; }

    public string? IncluirIepsTrasladado { get; set; }

    /// <summary>Complemento obligatorio para esta clave, si el SAT exige uno.</summary>
    public string? ComplementoQueDebeIncluir { get; set; }

    public bool EstimuloFranjaFronteriza { get; set; }

    /// <summary>
    /// Sinónimos que el propio SAT asocia a la clave. Se indexan junto con la descripción
    /// para que "gatos" encuentre "Gatos vivos" aunque el capturista no escriba la palabra
    /// exacta del catálogo.
    /// </summary>
    public string? PalabrasSimilares { get; set; }

    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}
