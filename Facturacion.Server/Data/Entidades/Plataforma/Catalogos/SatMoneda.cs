namespace Facturacion.Server.Data.Entidades.Plataforma.Catalogos;

/// <summary>Catálogo <c>c_Moneda</c>. Los decimales los exige CFDI 4.0 para redondear el importe con letra.</summary>
public sealed class SatMoneda : ISatCatalogoSimple
{
    public required string Clave { get; set; }
    public required string Descripcion { get; set; }
    public int Decimales { get; set; }
    public DateOnly FechaInicioVigencia { get; set; }
    public DateOnly? FechaFinVigencia { get; set; }
    public bool Vigente { get; set; }
}
