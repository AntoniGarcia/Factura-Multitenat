namespace Facturacion.Shared.Plataforma;

public static class ClavesSatPredeterminadasDePaquetes
{
    public const string ProductoServicio = "81112106";
    public const string UnidadServicio = "E48";

    public static string Producto(string? clave)
        => string.IsNullOrWhiteSpace(clave) ? ProductoServicio : clave.Trim();

    public static string Unidad(string? clave)
        => string.IsNullOrWhiteSpace(clave) ? UnidadServicio : clave.Trim();
}
