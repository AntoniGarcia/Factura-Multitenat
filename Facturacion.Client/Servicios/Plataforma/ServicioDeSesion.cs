using System.Net.Http.Json;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>
/// La sesión del navegador.
///
/// <para><b>El access token vive solo en memoria</b></para>
/// Nunca en <c>localStorage</c> ni en <c>sessionStorage</c> (ARQUITECTURA.md §4). Al recargar la
/// página se pierde, y eso es correcto: se recupera con la cookie de refresh, que el
/// JavaScript de la página no puede leer.
///
/// <para><b>Un solo refresh en vuelo</b></para>
/// Es la parte delicada. Si dos peticiones caducan a la vez y cada una dispara su propio
/// refresh, la segunda presenta un token que la primera ya consumió; el servidor lo toma
/// —con razón— por una cookie robada, invalida la familia completa y el usuario queda fuera
/// sin haber hecho nada. Por eso el primero en llegar crea la tarea y los demás esperan
/// <b>esa misma tarea</b> en vez de empezar otra.
///
/// <para>
/// Usa un <c>HttpClient</c> sin el manejador de autenticación: si el refresh saliera por él,
/// un 401 del propio refresh dispararía otro refresh.
/// </para>
/// </summary>
public sealed class ServicioDeSesion(IHttpClientFactory fabrica)
{
    public const string ClienteDesnudo = "auth";

    private readonly SemaphoreSlim _candado = new(1, 1);

    private Task<bool>? _refrescoEnVuelo;
    private string? _accessToken;
    private DateTime _expiraUtc;

    /// <summary>Se dispara cuando la sesión cambia, para que la interfaz se entere.</summary>
    public event Action? Cambio;

    public SesionDto? Sesion { get; private set; }

    public bool HaySesion => Sesion is not null && _accessToken is not null;

    /// <summary>Token para el encabezado <c>Authorization</c>. Nulo si no hay sesión.</summary>
    public string? Token => _accessToken;

    public bool TokenVigente => _accessToken is not null && DateTime.UtcNow < _expiraUtc;

    public bool TienePermiso(string permiso) => Sesion?.Permisos.Contains(permiso) ?? false;

    /// <summary>Devuelve el problema si algo falló, o <c>null</c> si la sesión quedó abierta.</summary>
    public async Task<DetalleProblema?> IniciarSesionAsync(PeticionInicioSesion peticion)
    {
        var respuesta = await Cliente().PostAsJsonAsync("api/auth/iniciar-sesion", peticion);

        if (!respuesta.IsSuccessStatusCode)
            return await LeerProblema(respuesta);

        await Guardar(respuesta);
        return null;
    }

    /// <summary>
    /// Alta de cuenta. <b>No inicia sesión</b>: la contraseña se manda por correo, así que
    /// aquí no hay ninguna con la que entrar. Devuelve el mensaje del servidor, que es el
    /// mismo exista o no el correo.
    /// </summary>
    public async Task<(RespuestaRegistro? Exito, DetalleProblema? Error)> RegistrarAsync(PeticionRegistro peticion)
    {
        var respuesta = await Cliente().PostAsJsonAsync("api/auth/registro", peticion);

        return respuesta.IsSuccessStatusCode
            ? (await respuesta.Content.ReadFromJsonAsync<RespuestaRegistro>(), null)
            : (null, await LeerProblema(respuesta));
    }

    /// <summary>
    /// Intenta recuperar la sesión con la cookie. La usa la puerta de arranque y también el
    /// manejador cuando el servidor responde 401.
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
                // Se compara por referencia: si mientras tanto arrancó otro refresco, este
                // no debe borrarlo.
                if (ReferenceEquals(_refrescoEnVuelo, enVuelo)) _refrescoEnVuelo = null;
            }
            finally
            {
                _candado.Release();
            }
        }
    }

    public async Task<DetalleProblema?> CambiarEmpresaAsync(Guid empresaId)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, "api/auth/cambiar-empresa")
        {
            Content = JsonContent.Create(new PeticionCambioEmpresa(empresaId))
        };

        peticion.Headers.Authorization = new("Bearer", _accessToken);

        var respuesta = await Cliente().SendAsync(peticion);

        if (!respuesta.IsSuccessStatusCode)
            return await LeerProblema(respuesta);

        await Guardar(respuesta);
        return null;
    }

    public async Task CerrarSesionAsync()
    {
        try
        {
            await Cliente().PostAsync("api/auth/cerrar-sesion", content: null);
        }
        finally
        {
            // Aunque el servidor no conteste, la sesión de este navegador se cierra: dejar
            // el token en memoria tras un cierre de sesión sería peor que el error de red.
            Olvidar();
        }
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
            var respuesta = await Cliente().PostAsync("api/auth/refresh", content: null);

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
            // Sin red no se puede afirmar que la sesión terminó, pero tampoco se puede
            // seguir: se olvida y la pantalla de inicio dirá lo que pasa.
            Olvidar();
            return false;
        }
    }

    private async Task Guardar(HttpResponseMessage respuesta)
    {
        var sesion = await respuesta.Content.ReadFromJsonAsync<RespuestaSesion>();

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
