namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>
/// Catálogo <c>c_UsoCFDI</c>, con la matriz de compatibilidad que trae el propio archivo del
/// SAT: la columna "Régimen Fiscal Receptor" lista, separadas por coma, las claves de
/// <see cref="SatRegimenFiscal"/> con las que ese uso es válido. Es la validación que más
/// timbrados salva (ARQUITECTURA.md §7) y viene lista para usarse, no hay que reconstruirla a mano.
/// </summary>
public sealed class SatUsoCfdi : ISatCatalogoSimple
{
    public required string Clave { get; set; }
    public required string Descripcion { get; set; }
    public bool AplicaFisica { get; set; }
    public bool AplicaMoral { get; set; }

    /// <summary>
    /// Claves de régimen fiscal compatibles, tal como las publica el SAT: separadas por coma,
    /// sin normalizar espacios. <see cref="EsCompatibleConRegimen"/> es quien las interpreta;
    /// nadie más debería parsear esta cadena directamente.
    /// </summary>
    public required string RegimenesFiscalesAplicables { get; set; }

    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }

    public bool EsCompatibleConRegimen(string claveRegimen)
        => RegimenesFiscalesAplicables
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(r => r.Equals(claveRegimen, StringComparison.OrdinalIgnoreCase));
}
