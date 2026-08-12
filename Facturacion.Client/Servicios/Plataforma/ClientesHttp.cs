namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>
/// Nombres de los clientes HTTP registrados. Existen dos y la diferencia importa:
/// <list type="bullet">
///   <item><description>
///     <see cref="Api"/> — el de todo el sistema. Adjunta el access token y, ante un 401,
///     refresca una vez y reintenta.
///   </description></item>
///   <item><description>
///     <c>ServicioDeSesion.ClienteDesnudo</c> — solo para el circuito de identidad. Sin
///     manejador, porque un 401 del refresh no debe disparar otro refresh.
///   </description></item>
/// </list>
/// </summary>
public static class ClientesHttp
{
    public const string Api = "api";
}
