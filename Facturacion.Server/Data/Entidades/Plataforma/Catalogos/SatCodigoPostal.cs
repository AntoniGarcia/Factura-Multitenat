namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>
/// Catálogo <c>c_CodigoPostal</c> (~95,700 códigos, repartido en dos hojas en el archivo del
/// SAT). Es el que valida <c>DomicilioFiscalReceptor</c>, obligatorio en CFDI 4.0
/// (CLAUDE.md §7).
/// <para>
/// No guarda el nombre de la localidad: el archivo del SAT trae ese catálogo aparte
/// (<c>C_Localidad</c>) pero nada en el alcance de esta fase necesita mostrarlo —es relevante
/// para Carta Porte, fuera del MVP (REPARTO-EQUIPO.md §1)—, así que no se carga.
/// </para>
/// </summary>
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
