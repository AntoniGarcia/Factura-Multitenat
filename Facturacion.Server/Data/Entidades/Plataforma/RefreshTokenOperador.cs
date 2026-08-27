namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Refresh token de una sesión de operador. Misma mecánica que el del inquilino: rotación en
/// cada uso y familia por sesión, de modo que un token ya consumido invalida la familia
/// entera (ARQUITECTURA.md §4).
///
/// <para><b>Por qué una tabla propia y no reutilizar <see cref="RefreshToken"/></b></para>
/// Aquella tiene llave foránea real contra <see cref="Usuario"/>, así que un operador no cabe
/// en ella. Pero la razón de fondo es otra: compartir tabla dejaría que el canje de refresh
/// del inquilino resolviera un token de operador, y bastaría un descuido en un <c>Where</c>
/// para cruzar las dos identidades. Separadas, ese error no se puede escribir.
///
/// <para>
/// No lleva empresa activa —el operador no tiene tenencia— ni <c>EmpresaId</c>, así que queda
/// fuera del filtro global.
/// </para>
/// </summary>
public sealed class RefreshTokenOperador
{
    public Guid Id { get; set; }

    /// <summary>Identifica la sesión completa. Se invalida entera ante una reutilización.</summary>
    public Guid FamiliaId { get; set; }

    public Guid OperadorId { get; set; }

    public OperadorPlataforma Operador { get; set; } = null!;

    /// <summary>SHA-256 del token en base64; el token en claro no se guarda nunca.</summary>
    public required string HashToken { get; set; }

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
