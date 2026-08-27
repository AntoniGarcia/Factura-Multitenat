using System.Net;
using System.Net.Http.Headers;

namespace Facturacion.Client.Servicios.Operador;

/// <summary>
/// Adjunta el token del panel de operador a cada petición y, ante un 401, refresca una sola
/// vez y reintenta.
/// <para>
/// Es el gemelo del manejador del inquilino, sobre la otra sesión. Están separados por lo
/// mismo que las cookies y las tablas: un solo manejador que eligiera token según la ruta
/// sería un sitio donde equivocarse y mandar la credencial del proveedor a un endpoint de
/// inquilino, o al revés.
/// </para>
/// </summary>
public sealed class ManejadorDeAutenticacionDeOperador(ServicioDeSesionDeOperador sesion) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage peticion, CancellationToken ct)
    {
        // El cuerpo se guarda antes del primer envío: un HttpRequestMessage no se puede
        // reenviar, y sin esto el reintento saldría sin contenido.
        if (peticion.Content is not null)
            await peticion.Content.LoadIntoBufferAsync(ct);

        Adjuntar(peticion);

        var respuesta = await base.SendAsync(peticion, ct);

        if (respuesta.StatusCode != HttpStatusCode.Unauthorized)
            return respuesta;

        respuesta.Dispose();

        if (!await sesion.RefrescarAsync())
        {
            sesion.Olvidar();
            return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = peticion };
        }

        var reintento = await Clonar(peticion, ct);
        Adjuntar(reintento);

        return await base.SendAsync(reintento, ct);
    }

    private void Adjuntar(HttpRequestMessage peticion)
    {
        if (sesion.Token is { } token)
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<HttpRequestMessage> Clonar(HttpRequestMessage original, CancellationToken ct)
    {
        var copia = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version
        };

        if (original.Content is not null)
        {
            var cuerpo = new MemoryStream();
            await original.Content.CopyToAsync(cuerpo, ct);
            cuerpo.Position = 0;

            copia.Content = new StreamContent(cuerpo);

            foreach (var cabecera in original.Content.Headers)
                copia.Content.Headers.TryAddWithoutValidation(cabecera.Key, cabecera.Value);
        }

        foreach (var cabecera in original.Headers)
            copia.Headers.TryAddWithoutValidation(cabecera.Key, cabecera.Value);

        foreach (var propiedad in original.Options)
            copia.Options.TryAdd(propiedad.Key, propiedad.Value);

        return copia;
    }
}
