using System.Net.Http.Json;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;

namespace Facturacion.Client.Servicios.Operador;

/// <summary>
/// La sesión del panel de operador en este navegador. Mismas reglas que la del inquilino: el
/// access token vive solo en memoria y se recupera con la cookie de refresh, que el
/// JavaScript de la página no puede leer (ARQUITECTURA.md §4).
///
/// <para><b>Un solo refresco en vuelo</b></para>
/// Igual de imprescindible que en el otro lado: dos refrescos simultáneos harían que el
/// segundo presentara un token ya consumido, el servidor lo tomaría por una cookie robada
/// —con razón— e invalidaría la sesión entera sin que nadie hiciera nada malo.
///
/// <para><b>Las dos sesiones no conviven</b></para>
/// Entrar como operador olvida la sesión de inquilino y al revés. Nadie es las dos cosas a la
/// vez, y sostener ambas en la misma pestaña solo daría lugar a dudas sobre con qué identidad
/// se está actuando. Quien necesite las dos, usa otra ventana.
/// </summary>
public sealed class ServicioDeSesionDeOperador(IHttpClientFactory fabrica)
{
    public const string ClienteDesnudo = "auth-operador";

    private readonly SemaphoreSlim _candado = new(1, 1);

    private Task<bool>? _refrescoEnVuelo;
    private string? _accessToken;
    private DateTime _expiraUtc;

    /// <summary>Se dispara cuando la sesión cambia, para que la interfaz se entere.</summary>
    public event Action? Cambio;

    public SesionDeOperadorDto? Sesion { get; private set; }

    public bool HaySesion => Sesion is not null && _accessToken is not null;

    public string? Token => _accessToken;

    public bool TokenVigente => _accessToken is not null && DateTime.UtcNow < _expiraUtc;

    /// <summary>Devuelve el problema si algo falló, o <c>null</c> si la sesión quedó abierta.</summary>
    public async Task<DetalleProblema?> IniciarSesionAsync(PeticionInicioSesionOperador peticion)
    {
        var respuesta = await Cliente().PostAsJsonAsync("api/operador/auth/iniciar-sesion", peticion);

        if (!respuesta.IsSuccessStatusCode)
            return await LeerProblema(respuesta);

        await Guardar(respuesta);
        return null;
    }

    /// <summary>
    /// Intenta recuperar la sesión con la cookie. La usan la puerta de arranque y el manejador
    /// cuando el servidor responde 401.
    /// </summary>
    public async Task<bool> RefrescarAsync()
    {
        Task<bool> enVuelo;

        await _candado.WaitAsync();
        try
        {
            _refrescoEnVuelo ??= EjecutarRefresco();
            enVuelo = _refrescoEnVuelo;
        }
        finally
        {
            _candado.Release();
        }

        try
        {
            return await enVuelo;
        }
        finally
        {
            await _candado.WaitAsync();
            try
            {
                // Por referencia: si mientras tanto arrancó otro refresco, este no debe borrarlo.
                if (ReferenceEquals(_refrescoEnVuelo, enVuelo)) _refrescoEnVuelo = null;
            }
            finally
            {
                _candado.Release();
            }
        }
    }

    public async Task CerrarSesionAsync()
    {
        try
        {
            await Cliente().PostAsync("api/operador/auth/cerrar-sesion", content: null);
        }
        finally
        {
            // Aunque el servidor no conteste, la sesión de este navegador se cierra.
            Olvidar();
        }
    }

    /// <summary>
    /// Actualiza en memoria los datos del operador tras un cambio de perfil, para que la
    /// cabecera y la pantalla se enteren sin volver a pedir la sesión.
    /// </summary>
    public void RefrescarDatos(SesionDeOperadorDto sesion)
    {
        Sesion = sesion;
        Cambio?.Invoke();
    }

    /// <summary>Borra la sesión de memoria sin llamar al servidor.</summary>
    public void Olvidar()
    {
        _accessToken = null;
        _expiraUtc = default;
        Sesion = null;
        Cambio?.Invoke();
    }

    private async Task<bool> EjecutarRefresco()
    {
        try
        {
            var respuesta = await Cliente().PostAsync("api/operador/auth/refresh", content: null);

            if (!respuesta.IsSuccessStatusCode)
            {
                Olvidar();
                return false;
            }

            await Guardar(respuesta);
            return true;
        }
        catch (HttpRequestException)
        {
            Olvidar();
            return false;
        }
    }

    private async Task Guardar(HttpResponseMessage respuesta)
    {
        var sesion = await respuesta.Content.ReadFromJsonAsync<RespuestaSesionDeOperador>();

        if (sesion is null)
        {
            Olvidar();
            return;
        }

        _accessToken = sesion.AccessToken;
        _expiraUtc = sesion.ExpiraUtc;
        Sesion = sesion.Sesion;

        Cambio?.Invoke();
    }

    private static async Task<DetalleProblema> LeerProblema(HttpResponseMessage respuesta)
    {
        try
        {
            var problema = await respuesta.Content.ReadFromJsonAsync<DetalleProblema>();
            if (problema is not null) return problema;
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or System.Text.Json.JsonException)
        {
            // El cuerpo no era Problem Details: se cae al genérico de abajo.
        }

        return new DetalleProblema(
            "urn:facturacion:error:respuesta-inesperada",
            "No se pudo completar la operación",
            (int)respuesta.StatusCode,
            "El servidor respondió algo que no se pudo interpretar.",
            null,
            string.Empty,
            null);
    }

    private HttpClient Cliente() => fabrica.CreateClient(ClienteDesnudo);
}
