namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>Operador de la plataforma, independiente de las cuentas y empresas emisoras.</summary>
public sealed class OperadorPlataforma
{
    public Guid Id { get; set; }

    public required string Nombre { get; set; }

    public required string Correo { get; set; }

    /// <summary>
    /// El correo en mayúsculas, que es por donde entra la búsqueda al iniciar sesión. Se
    /// guarda aparte para que el índice único sea insensible a mayúsculas sin depender de la
    /// intercalación de la base.
    /// </summary>
    public required string CorreoNormalizado { get; set; }

    /// <summary>Resultado de <c>IPasswordHasher</c>. La contraseña en claro no se guarda nunca.</summary>
    public required string HashContrasena { get; set; }

    /// <summary>
    /// Un operador desactivado no entra, pero sus registros de bitácora siguen apuntando a él:
    /// nada se borra, y menos quien acreditó un pago.
    /// </summary>
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Dueño del SaaS (el que creó la BD). No se puede desactivar ni eliminar desde el panel
    /// para evitar que cualquier sesión, incluyendo la suya, deje la plataforma sin administrador.
    /// Se marca en el alta inicial del asistente o en el primer alta por consola.
    /// </summary>
    public bool EsPrincipal { get; set; }

    public DateTime FechaAltaUtc { get; set; }

    public DateTime? UltimoAccesoUtc { get; set; }

    /// <summary>Intentos fallidos desde el último acceso bueno. Se reinicia al entrar.</summary>
    public int AccesosFallidos { get; set; }

    /// <summary>
    /// Hasta cuándo está cerrada la puerta. El bloqueo crece con cada tanda de fallos, igual
    /// que para los inquilinos: aquí no se puede usar el de Identity porque está atado a
    /// <see cref="Usuario"/>.
    /// </summary>
    public DateTime? BloqueadoHastaUtc { get; set; }

    /// <summary>Bloqueos seguidos sin un acceso bueno de por medio; alarga el siguiente.</summary>
    public int BloqueosConsecutivos { get; set; }

    public ICollection<RefreshTokenOperador> RefreshTokens { get; set; } = [];

    /// <summary>Permisos asignados a este operador. Claves de <see cref="Facturacion.Shared.Comun.Permisos"/>.</summary>
    public ICollection<PermisoOperador> Permisos { get; set; } = [];
}
