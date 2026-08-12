namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// El contratante que paga la suscripción. Una cuenta puede tener varias empresas emisoras
/// —por ejemplo una llantera y una cementera del mismo dueño— y cada una aísla por completo
/// sus datos.
/// </summary>
public sealed class Cuenta
{
    public Guid Id { get; set; }

    public required string Nombre { get; set; }

    public required string CorreoContacto { get; set; }

    public bool Activa { get; set; } = true;

    public DateTime FechaAltaUtc { get; set; }

    public ICollection<Empresa> Empresas { get; set; } = [];

    public ICollection<Usuario> Usuarios { get; set; } = [];
}
