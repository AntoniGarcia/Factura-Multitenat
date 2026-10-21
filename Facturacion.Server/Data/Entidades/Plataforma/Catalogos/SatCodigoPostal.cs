namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>Catálogo de códigos postales para validar el domicilio fiscal. Las claves de localidad se resuelven con su catálogo.</summary>
public sealed class SatCodigoPostal
{
    /// <summary>Cinco dígitos.</summary>
    public required string Clave { get; set; }

    public required string ClaveEstado { get; set; }

    /// <summary>Nulo cuando el SAT no lo tiene resuelto para este código postal.</summary>
    public string? ClaveMunicipio { get; set; }

    /// <summary>Clave de <c>C_Localidad</c>, sin catálogo de nombres cargado; ver comentario de la clase.</summary>
    public string? ClaveLocalidad { get; set; }

    public bool EstimuloFranjaFronteriza { get; set; }

    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}
