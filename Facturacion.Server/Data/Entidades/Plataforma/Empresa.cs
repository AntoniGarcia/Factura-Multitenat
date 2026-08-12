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
/// La fase 4 le agrega el resto de los campos de §4 del documento funcional: domicilio,
/// contacto, logo, tasas por omisión y certificados.
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

    // Licencias de los módulos de la fase 2. Se guardan desde ahora para que agregar esos
    // módulos no obligue a rehacer el esquema; hoy no habilitan nada.
    public bool LicNotarios { get; set; }

    public bool LicObras { get; set; }

    public bool LicComercio { get; set; }

    public bool LicINE { get; set; }

    public ICollection<UsuarioEmpresa> Usuarios { get; set; } = [];
}
