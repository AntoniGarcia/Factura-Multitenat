namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>Catálogo <c>c_TipoDeComprobante</c> (I, E, T, N, P...).</summary>
public sealed class SatTipoDeComprobante : ISatCatalogoSimple
{
    public required string Clave { get; set; }
    public required string Descripcion { get; set; }

    /// <summary>Importe máximo permitido para este tipo. Nulo cuando el SAT no lo acota.</summary>
    public decimal? ValorMaximo { get; set; }

    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}
