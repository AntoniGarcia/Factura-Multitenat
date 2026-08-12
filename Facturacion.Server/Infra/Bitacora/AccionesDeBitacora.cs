namespace Facturacion.Server.Infra.Bitacora;

/// <summary>
/// Acciones de la bitácora. Son cadenas fijas para poder filtrarlas después sin adivinar
/// cómo las escribió cada quien.
/// </summary>
public static class AccionesDeBitacora
{
    public const string InicioSesion = "inicio_sesion";
    public const string InicioSesionFallido = "inicio_sesion_fallido";
    public const string CierreSesion = "cierre_sesion";
    public const string TokenRotado = "token_rotado";
    public const string FamiliaInvalidada = "familia_invalidada";
    public const string EmpresaCambiada = "empresa_cambiada";

    /// <summary>
    /// Reutilización de un refresh token ya consumido: la señal de que alguien copió la
    /// cookie. Se registra aparte del resto para poder buscarla sola.
    /// </summary>
    public const string ReutilizacionDeToken = "reutilizacion_de_token";
}

/// <summary>Entidades sobre las que registra la bitácora del circuito de identidad.</summary>
public static class EntidadesDeBitacora
{
    public const string Sesion = "Sesion";
    public const string RefreshToken = "RefreshToken";
}
