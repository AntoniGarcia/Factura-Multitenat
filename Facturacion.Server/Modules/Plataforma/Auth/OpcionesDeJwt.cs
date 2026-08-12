using System.ComponentModel.DataAnnotations;

namespace Facturacion.Server.Modules.Plataforma.Auth;

/// <summary>
/// Configuración de la emisión de tokens. La clave de firma viene de configuración, nunca
/// del repositorio: en desarrollo de <c>appsettings.Development.json</c>, que está en el
/// <c>.gitignore</c>, y en producción de una variable de entorno.
/// </summary>
public sealed class OpcionesDeJwt
{
    public const string Seccion = "Jwt";

    [Required]
    public string Emisor { get; set; } = string.Empty;

    [Required]
    public string Audiencia { get; set; } = string.Empty;

    /// <summary>
    /// Clave de firma HMAC. Se exigen 32 caracteres como mínimo porque HS256 con una clave
    /// más corta que su propia salida es falsa seguridad.
    /// </summary>
    [Required, MinLength(32)]
    public string ClaveDeFirma { get; set; } = string.Empty;

    /// <summary>Vida del access token. CLAUDE.md §4 la fija en 15 minutos.</summary>
    [Range(1, 15)]
    public int MinutosDeVida { get; set; } = 15;

    /// <summary>Duración de la cookie de refresh sin "mantener sesión iniciada".</summary>
    [Range(1, 24)]
    public int HorasDeSesionCorta { get; set; } = 12;

    /// <summary>Duración de la cookie de refresh con "mantener sesión iniciada".</summary>
    [Range(1, 90)]
    public int DiasDeSesionLarga { get; set; } = 30;
}
