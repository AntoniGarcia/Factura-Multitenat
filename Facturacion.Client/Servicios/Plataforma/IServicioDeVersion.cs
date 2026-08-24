using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>
/// Compara la versión con la que se compiló este WebAssembly contra la que el servidor
/// dice tener ahora mismo. Un catálogo del SAT cacheado y viejo produce comprobantes mal
/// emitidos: por eso <c>GuardaDeVersion</c> fuerza la recarga en cuanto difieren
/// (ARQUITECTURA.md §4).
/// </summary>
public interface IServicioDeVersion
{
    string VersionCompilada { get; }

    /// <summary>Devuelve la versión del servidor, o <c>null</c> si la petición falló.</summary>
    Task<string?> ObtenerVersionDelServidorAsync(CancellationToken ct);
}

public sealed class ServicioDeVersion(IHttpClientFactory fabrica) : IServicioDeVersion
{
    public string VersionCompilada { get; } = LeerVersionCompilada();

    public async Task<string?> ObtenerVersionDelServidorAsync(CancellationToken ct)
    {
        try
        {
            // El cliente desnudo: sin token, porque /api/version es anónimo y tiene que
            // poder consultarse incluso antes de iniciar sesión.
            var cliente = fabrica.CreateClient(ServicioDeSesion.ClienteDesnudo);
            var respuesta = await cliente.GetFromJsonAsync<RespuestaVersion>("api/version", ct);
            return respuesta?.Version;
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or TaskCanceledException)
        {
            // Sin red no se puede comparar nada; no es un desajuste de versión, es que no
            // hay con qué comparar todavía.
            return null;
        }
    }

    private static string LeerVersionCompilada()
    {
        var informativa = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrEmpty(informativa)) return "desconocida";

        // La versión informativa trae el hash del commit después de un '+', igual que en
        // el Server; se recorta para comparar lo mismo que /api/version devuelve.
        var separador = informativa.IndexOf('+');
        return separador < 0 ? informativa : informativa[..separador];
    }

    private sealed record RespuestaVersion([property: JsonPropertyName("version")] string Version);
}
