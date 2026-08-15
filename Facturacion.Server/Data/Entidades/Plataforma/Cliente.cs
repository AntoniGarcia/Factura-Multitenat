namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Receptor de comprobantes. Es donde viven las reglas de CFDI 4.0 que más timbrados rompen
/// (CLAUDE.md §7): nombre exacto de la constancia, código postal del domicilio fiscal,
/// régimen fiscal y uso de CFDI compatible.
///
/// <para><b>Dos códigos postales, y no es redundancia</b></para>
/// <see cref="DomicilioFiscalCp"/> es el de la <b>constancia</b> y es el que viaja al CFDI
/// como <c>DomicilioFiscalReceptor</c>; es obligatorio. <see cref="CodigoPostal"/> es el del
/// domicilio de entrega y solo sale en el PDF. El sistema viejo (§8 del documento funcional)
/// tenía un solo campo «C.P.», y confundirlos produce comprobantes rechazados: el SAT
/// compara ese dato contra el padrón.
///
/// <para><b>Nada se borra</b></para>
/// Baja lógica con <see cref="Activo"/>. Un cliente inactivo sigue existiendo porque sus
/// comprobantes viejos tienen que poder explicarse (CLAUDE.md §5).
/// </summary>
public sealed class Cliente : IEntidadDeEmpresa
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Empresa Empresa { get; set; } = null!;

    /// <summary>
    /// Consecutivo por empresa. No tiene valor fiscal, pero los contadores se la memorizan y
    /// la usan para buscar: el sistema viejo la tenía y quitarla obligaría a reaprender el
    /// catálogo.
    /// </summary>
    public int ClaveInterna { get; set; }

    // ── Datos fiscales: obligatorios en CFDI 4.0 ───────────────────────────────────────
    public required string Rfc { get; set; }

    /// <summary>
    /// Nombre ya normalizado: mayúsculas, sin acentos y sin régimen de capital. Tiene que
    /// coincidir <b>exactamente</b> con la Constancia de Situación Fiscal.
    /// </summary>
    public required string Nombre { get; set; }

    /// <summary>Clave de <c>c_RegimenFiscal</c>.</summary>
    public required string RegimenFiscal { get; set; }

    /// <summary>Código postal de la constancia. Va al CFDI como <c>DomicilioFiscalReceptor</c>.</summary>
    public required string DomicilioFiscalCp { get; set; }

    /// <summary>
    /// Clave de <c>c_Pais</c>. Solo se llena para el genérico de extranjero
    /// <c>XEXX010101000</c>; en cualquier otro RFC no aplica.
    /// </summary>
    public string? ResidenciaFiscal { get; set; }

    /// <summary>Registro de identidad tributaria del extranjero. Acompaña a <see cref="ResidenciaFiscal"/>.</summary>
    public string? NumRegIdTrib { get; set; }

    // ── Preferencias: precargan el formulario de facturación, no lo deciden ─────────────
    public string? UsoCfdiPreferido { get; set; }

    public string? MetodoPagoPreferido { get; set; }

    public string? FormaPagoPreferida { get; set; }

    // ── Contacto ───────────────────────────────────────────────────────────────────────
    public string? Telefono { get; set; }

    /// <summary>Destinatario por omisión al enviar el comprobante por correo.</summary>
    public string? CorreoPrincipal { get; set; }

    // ── Domicilio de entrega: informativo, sale en el PDF ───────────────────────────────
    public string? Calle { get; set; }

    public string? NumeroExterior { get; set; }

    public string? NumeroInterior { get; set; }

    public string? Colonia { get; set; }

    public string? Localidad { get; set; }

    public string? Referencia { get; set; }

    public string? Municipio { get; set; }

    public string? Estado { get; set; }

    public string? Pais { get; set; }

    /// <summary>Código postal del domicilio de entrega. <b>No</b> es <see cref="DomicilioFiscalCp"/>.</summary>
    public string? CodigoPostal { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime FechaAltaUtc { get; set; }

    public DateTime? FechaModificacionUtc { get; set; }
}
