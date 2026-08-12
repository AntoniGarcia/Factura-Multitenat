using System.Net;
using System.Net.Http.Headers;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>
/// Adjunta el access token a cada petición y, ante un 401, refresca <b>una sola vez</b> y
/// reintenta la petición original.
/// <para>
/// El refresco lo coordina <see cref="ServicioDeSesion"/>: si varias peticiones caducan a la
/// vez, todas esperan el mismo refresco en lugar de disparar uno cada una.
/// </para>
/// <para>
/// Lo usa el cliente general de la API. Las llamadas del propio circuito de identidad salen
/// por un cliente sin este manejador, para que un 401 del refresh no dispare otro refresh.
/// </para>
/// </summary>
public sealed class ManejadorDeAutenticacion(ServicioDeSesion sesion) : DelegatingHandler
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
