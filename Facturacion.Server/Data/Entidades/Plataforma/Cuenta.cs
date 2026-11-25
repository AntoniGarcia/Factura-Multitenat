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

    public const int UsuariosPorOmision = 3;

    public const int EmpresasPorOmision = 1;

    public bool Activa { get; set; } = true;

    // Usuarios con acceso activo, contando al titular. Lo fija el operador.
    public int LimiteDeUsuarios { get; set; } = UsuariosPorOmision;

    // Empresas que puede dar de alta el titular, activas o no. Lo fija el operador.
    public int LimiteDeEmpresas { get; set; } = EmpresasPorOmision;

    public DateTime FechaAltaUtc { get; set; }

    public ICollection<Empresa> Empresas { get; set; } = [];

    public ICollection<Usuario> Usuarios { get; set; } = [];
}
