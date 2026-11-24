namespace Facturacion.Shared.Documentos;

/// Clase de factura de ingreso; se elige al crear el borrador y no cambia.
/// Decide las pestañas del formulario y qué datos acepta el servidor.
/// Pagos y traslados la llevan nula.
public static class VariantesDeFactura
{
    public const string Basica = "basica";
    public const string Notaria = "notaria";
    public const string ComercioExterior = "comercio_exterior";

    // Estimación de obra con impuestos locales (5 al millar y otras deducciones).
    public const string Obra = "obra";

    public static IReadOnlyList<string> Todas { get; } = [Basica, Notaria, ComercioExterior, Obra];

    public static bool EsValida(string? variante) => variante is not null && Todas.Contains(variante);
}

// Variante nula crea una factura básica, como /facturas/nueva sin variante.
public sealed record PeticionCrearBorrador(string? Variante);
