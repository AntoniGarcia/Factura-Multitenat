namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Anualidad de suscripción al servicio. Es de la <b>cuenta</b>, no de la empresa: quien
/// contrata el SaaS es el contratante, y sus tres empresas emisoras viven bajo la misma
/// suscripción.
///
/// <para><b>Por eso no lleva <c>EmpresaId</c> ni filtro global</b></para>
/// Al no implementar <c>IEntidadDeEmpresa</c> queda fuera del filtro global de EF, así que
/// toda consulta contra esta tabla tiene que acotar por <see cref="CuentaId"/> a mano. Es la
/// única forma de que un usuario vea la membresía de su cuenta sin importar en qué empresa
/// esté parado, y la razón de que el servicio la resuelva desde la empresa activa hacia su
/// cuenta en vez de aceptar un identificador del cliente.
/// </summary>
public sealed class Membresia
{
    public Guid Id { get; set; }

    public Guid CuentaId { get; set; }

    public Cuenta Cuenta { get; set; } = null!;

    public DateTime InicioUtc { get; set; }

    public DateTime FinUtc { get; set; }

    /// <summary>Uno de <see cref="EstadosDeMembresia"/>.</summary>
    public required string Estado { get; set; }

    public DateTime CreadaUtc { get; set; }
}

/// <summary>Estados de una membresía. Cadenas fijas para poder filtrarlas sin adivinar.</summary>
public static class EstadosDeMembresia
{
    public const string Activa = "activa";

    /// <summary>Pasó su fecha de fin sin renovarse.</summary>
    public const string Vencida = "vencida";

    /// <summary>Se dio de baja antes de su fecha de fin.</summary>
    public const string Cancelada = "cancelada";
}
