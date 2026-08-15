namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Empresa emisora. Es la unidad de aislamiento: clientes, productos, series y comprobantes
/// cuelgan de ella.
/// <para>
/// No implementa <see cref="IEntidadDeEmpresa"/>: filtrar las empresas por la empresa activa
/// haría imposible listar las empresas del usuario, que es justo lo que necesita el selector
/// de la barra superior. Se acota siempre por <c>CuentaId</c>.
/// </para>
/// <para>
/// Los datos <b>fiscales</b> son los que viajan al CFDI y son obligatorios. El domicilio y
/// el contacto son informativos —salen en el PDF, no en el XML— y por eso son opcionales:
/// obligar a capturarlos para poder timbrar sería inventarse un requisito que el SAT no pone.
/// </para>
/// </summary>
public sealed class Empresa
{
    public Guid Id { get; set; }

    public Guid CuentaId { get; set; }

    public Cuenta Cuenta { get; set; } = null!;

    public required string Rfc { get; set; }

    /// <summary>Razón social ya normalizada como exige CFDI 4.0.</summary>
    public required string NombreFiscal { get; set; }

    /// <summary>Clave de <c>c_RegimenFiscal</c>. La fase 3 le pondrá la llave foránea al catálogo.</summary>
    public required string RegimenFiscal { get; set; }

    /// <summary>Código postal del lugar de expedición.</summary>
    public required string CodigoPostalExpedicion { get; set; }

    /// <summary>
    /// Huso horario del lugar de expedición. La base guarda todo en UTC; esto es lo que
    /// permite mostrar y sellar con la hora local correcta.
    /// </summary>
    public required string ZonaHoraria { get; set; }

    public bool Activa { get; set; } = true;

    public DateTime FechaAltaUtc { get; set; }

    // ── Domicilio (§4 del documento funcional) — informativo, sale en el PDF ────────────
    public string? Calle { get; set; }

    public string? NumeroExterior { get; set; }

    public string? NumeroInterior { get; set; }

    public string? Referencia { get; set; }

    public string? Colonia { get; set; }

    public string? Localidad { get; set; }

    public string? Municipio { get; set; }

    public string? Estado { get; set; }

    public string? Pais { get; set; }

    /// <summary>
    /// Código postal del domicilio. <b>No</b> es el mismo que
    /// <see cref="CodigoPostalExpedicion"/>: ese es el que viaja al CFDI como
    /// <c>LugarExpedicion</c>, este solo se imprime. Suelen coincidir, pero no siempre —una
    /// empresa puede expedir desde una sucursal— y confundirlos produce comprobantes con el
    /// lugar de expedición equivocado.
    /// </summary>
    public string? CodigoPostal { get; set; }

    // ── Contacto ───────────────────────────────────────────────────────────────────────
    public string? Telefono { get; set; }

    public string? CorreoContacto { get; set; }

    // ── Logo ───────────────────────────────────────────────────────────────────────────
    /// <summary>Ruta dentro del almacén cifrado. Nulo si no se ha cargado ninguno.</summary>
    public string? LogoRuta { get; set; }

    /// <summary>Tipo verificado por el contenido real del archivo, no por su extensión.</summary>
    public string? LogoTipoMime { get; set; }

    public string? LogoNombreOriginal { get; set; }

    // Licencias de los módulos de la fase 2. Se guardan desde ahora para que agregar esos
    // módulos no obligue a rehacer el esquema; hoy no habilitan nada.
    public bool LicNotarios { get; set; }

    public bool LicObras { get; set; }

    public bool LicComercio { get; set; }

    public bool LicINE { get; set; }

    public ICollection<UsuarioEmpresa> Usuarios { get; set; } = [];
}
