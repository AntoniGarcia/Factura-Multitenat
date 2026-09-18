namespace Facturacion.Server.Modules.Plataforma.Timbres;

/// <summary>Separa el IVA de un precio que ya lo incluye, conservando seis decimales.</summary>
public static class DesgloseDeIva
{
    public const decimal TasaGeneral = 0.16m;

    public static (decimal Subtotal, decimal Iva) CalcularDesdeTotal(decimal total)
    {
        var subtotal = Math.Round(
            total / (1m + TasaGeneral), 6, MidpointRounding.AwayFromZero);
        return (subtotal, total - subtotal);
    }
}
