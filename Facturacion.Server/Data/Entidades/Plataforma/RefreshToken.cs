namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Refresh token con rotación en cada uso. Cada sesión es una <see cref="FamiliaId"/>:
/// si llega un token ya consumido, se invalida la familia completa, porque es la señal de
/// que alguien copió la cookie (ARQUITECTURA.md §4).
/// <para>
/// No lleva <c>EmpresaId</c> y queda fuera del filtro global a propósito: la sesión sobrevive
/// al cambio de empresa activa, que emite un access token nuevo pero no toca la cookie.
/// </para>
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }

    /// <summary>Identifica la sesión completa. Se invalida entera ante una reutilización.</summary>
    public Guid FamiliaId { get; set; }

    public Guid UsuarioId { get; set; }

    public Usuario Usuario { get; set; } = null!;

    /// <summary>
    /// SHA-256 del token en base64. El token en claro nunca se guarda: si se filtra la base,
    /// no se puede reconstruir ninguna sesión.
    /// </summary>
    public required string HashToken { get; set; }

    /// <summary>
    /// Empresa que el usuario tenía activa en esta sesión. Vive aquí, del lado del servidor,
    /// y no en el navegador: al refrescar, el token nuevo sale ya con ella, así que un F5 o
    /// una renovación a los quince minutos no devuelven al usuario al selector de empresa.
    /// <para>
    /// Que esté aquí no relaja la regla de ARQUITECTURA.md §4: el Client sigue sin enviar un
    /// identificador de empresa en ninguna petición salvo <c>/cambiar-empresa</c>.
    /// </para>
    /// </summary>
    public Guid? EmpresaActivaId { get; set; }

    public DateTime CreadoUtc { get; set; }

    public DateTime ExpiraUtc { get; set; }

    /// <summary>Momento en que se canjeó. Un canje repetido es un incidente de seguridad.</summary>
    public DateTime? ConsumidoUtc { get; set; }

    public DateTime? RevocadoUtc { get; set; }

    public string? MotivoRevocacion { get; set; }

    /// <summary>Token que lo sustituyó al rotar; permite reconstruir la cadena de la familia.</summary>
    public Guid? ReemplazadoPorId { get; set; }

    public string? IpCreacion { get; set; }

    public string? AgenteUsuario { get; set; }
}
