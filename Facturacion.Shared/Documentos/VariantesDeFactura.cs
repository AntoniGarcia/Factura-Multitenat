namespace Facturacion.Shared.Documentos;

/// <summary>
/// Qué clase de factura de ingreso es un comprobante: la básica o una con su complemento. Se
/// elige en «Nuevo comprobante», se guarda al crear el borrador y no cambia después. De ella
/// dependen las pestañas del formulario y qué datos acepta el servidor: una factura básica no
/// admite datos de Notaría, Obra ni Comercio Exterior aunque alguien los mande.
///
/// <para>
/// Solo las facturas (tipo <c>I</c>) tienen variante. Pagos y traslados la llevan nula: su tipo
/// de comprobante ya dice qué son.
/// </para>
/// </summary>
public static class VariantesDeFactura
{
    public const string Basica = "basica";
    public const string Notaria = "notaria";
    public const string ComercioExterior = "comercio_exterior";

    /// <summary>Estimación de obra con impuestos locales (5 al millar y demás deducciones).</summary>
    public const string Obra = "obra";

    public static IReadOnlyList<string> Todas { get; } = [Basica, Notaria, ComercioExterior, Obra];

    public static bool EsValida(string? variante) => variante is not null && Todas.Contains(variante);
}

/// <param name="Variante">
/// Una de <see cref="VariantesDeFactura"/>. Nula o ausente crea una factura básica, que es lo que
/// pide quien entra directo a <c>/facturas/nueva</c>.
/// </param>
public sealed record PeticionCrearBorrador(string? Variante);
