namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Quien opera el SaaS: administra los paquetes que se venden, acredita los pagos y consulta
/// a las cuentas contratantes. No es un usuario de ninguna empresa.
///
/// <para><b>Por qué una tabla aparte y no un <see cref="Usuario"/> con una bandera</b></para>
/// Un <see cref="Usuario"/> exige <c>CuentaId</c>: pertenece a una cuenta contratante. El
/// operador no pertenece a ninguna —es el dueño del sistema, no un inquilino— así que la
/// bandera obligaría a inventarle una cuenta ficticia y a recordar excluirla de todos los
/// listados de clientes. Con dos tablas, un operador no puede aparecer como cliente ni un
/// cliente ganar poderes de operador: lo impide el esquema y no la vigilancia.
///
/// <para><b>Por qué no deriva de IdentityUser</b></para>
/// Arrastraría <c>AspNetUsers</c>, donde <c>CuentaId</c> es obligatorio, y con él las tablas
/// que ARQUITECTURA.md §4 mantiene fuera del esquema. La contraseña se cifra con el mismo
/// PBKDF2 de Identity a través de <c>IPasswordHasher</c>, que se puede usar suelto.
///
/// <para>
/// No implementa <c>IEntidadDeEmpresa</c>: queda fuera del filtro global y del interceptor de
/// sellado, igual que <see cref="Cuenta"/> y <see cref="Empresa"/>.
/// </para>
/// </summary>
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
    /// nada se borra (ARQUITECTURA.md §5), y menos quien acreditó un pago.
    /// </summary>
    public bool Activo { get; set; } = true;

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
