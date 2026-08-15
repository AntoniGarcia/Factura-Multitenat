namespace Facturacion.Server.Infra.Idempotencia;

/// <summary>
/// Habilita el rebobinado del cuerpo en las peticiones que traen <c>Idempotency-Key</c>.
///
/// <para><b>Por qué hace falta y por qué aquí</b></para>
/// <see cref="FiltroDeIdempotencia"/> necesita el SHA-256 del cuerpo para distinguir un
/// reintento legítimo de una clave reciclada para otra compra. Pero un filtro de endpoint
/// recibe los argumentos <b>ya enlazados</b>: para cuando corre, el enlace de modelo ya leyó
/// el cuerpo hasta el final. Sobre un flujo no rebobinable eso deja el hash calculado sobre
/// cero bytes —idéntico en todas las peticiones—, y la protección contra la clave reciclada
/// queda inerte sin que nada falle a la vista.
/// <para>
/// <c>EnableBuffering</c> tiene que llamarse <b>antes</b> de que alguien lea el cuerpo, así
/// que no puede vivir dentro del filtro. Se limita a las peticiones que de verdad traen el
/// encabezado para que ninguna otra pague el costo de almacenar su cuerpo.
/// </para>
/// </summary>
public sealed class MiddlewareDeBufferDeIdempotencia(RequestDelegate siguiente)
{
    public Task InvokeAsync(HttpContext contexto)
    {
        if (contexto.Request.Headers.ContainsKey("Idempotency-Key"))
            contexto.Request.EnableBuffering();

        return siguiente(contexto);
    }
}
