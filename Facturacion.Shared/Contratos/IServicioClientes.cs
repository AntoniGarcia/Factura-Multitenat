namespace Facturacion.Shared.Contratos;

/// <summary>
/// Lo que la mitad B necesita del catálogo de clientes al momento de timbrar.
/// </summary>
public interface IServicioClientes
{
    /// <summary>
    /// Devuelve la foto fiscal completa del receptor, lista para congelarse dentro del
    /// comprobante. No devuelve la entidad: una vez timbrado, el comprobante no puede
    /// depender de un catálogo que cambia (ARQUITECTURA.md §5, inmutabilidad).
    /// Devuelve <c>null</c> si el cliente no existe o no es de la empresa activa.
    /// </summary>
    Task<ReceptorFiscalDto?> ObtenerParaTimbradoAsync(Guid clienteId, CancellationToken ct);
}

/// <summary>
/// Datos fiscales del receptor tal como van a viajar al CFDI 4.0.
/// </summary>
/// <param name="ClienteId">Cliente del que se tomó la foto.</param>
/// <param name="Rfc">RFC validado en formato y dígito verificador.</param>
/// <param name="Nombre">Nombre ya normalizado como exige 4.0: mayúsculas, sin acentos y sin régimen de capital.</param>
/// <param name="RegimenFiscal">Clave de <c>c_RegimenFiscal</c>. Obligatorio en 4.0.</param>
/// <param name="DomicilioFiscalCp">Código postal del domicilio fiscal (<c>DomicilioFiscalReceptor</c>). Obligatorio en 4.0, y es el de la constancia, no el de entrega.</param>
/// <param name="UsoCfdiPreferido">Preferencia del cliente para precargar el formulario; el uso definitivo lo decide quien emite.</param>
/// <param name="MetodoPagoPreferido">Clave de <c>c_MetodoPago</c>, para precargar.</param>
/// <param name="FormaPagoPreferida">Clave de <c>c_FormaPago</c>, para precargar.</param>
/// <param name="CorreoPrincipal">Destinatario por omisión al enviar el comprobante.</param>
/// <param name="ResidenciaFiscal">Clave de <c>c_Pais</c>. Solo se llena para el RFC genérico de extranjero <c>XEXX010101000</c>.</param>
/// <param name="NumRegIdTrib">Registro de identidad tributaria del extranjero. Acompaña a <paramref name="ResidenciaFiscal"/>.</param>
public sealed record ReceptorFiscalDto(
    Guid ClienteId,
    string Rfc,
    string Nombre,
    string RegimenFiscal,
    string DomicilioFiscalCp,
    string? UsoCfdiPreferido,
    string? MetodoPagoPreferido,
    string? FormaPagoPreferida,
    string? CorreoPrincipal,
    string? ResidenciaFiscal,
    string? NumRegIdTrib);
