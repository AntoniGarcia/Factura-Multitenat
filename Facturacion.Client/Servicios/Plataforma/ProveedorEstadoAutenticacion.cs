using System.Security.Claims;
using Facturacion.Client.Servicios.Operador;
using Facturacion.Shared.Comun;
using Microsoft.AspNetCore.Components.Authorization;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>
/// Traduce la sesión en memoria al <see cref="ClaimsPrincipal"/> que consumen
/// <c>AuthorizeView</c> y <c>[Authorize]</c>.
/// <para>
/// Lo que arma aquí es <b>comodidad visual</b>: sirve para no dibujar botones que el usuario
/// no puede usar. Quien decide de verdad es el servidor, que rechaza la operación aunque el
/// botón se haya dibujado.
/// </para>
/// <para>
/// Atiende a las dos identidades del sistema, pero nunca a la vez: si hay sesión de operador
/// se arma la suya y solo la suya. Es el reflejo en el navegador de que los dos tokens no
/// coexisten.
/// </para>
/// </summary>
public sealed class ProveedorEstadoAutenticacion : AuthenticationStateProvider, IDisposable
{
    private static readonly AuthenticationState Invitado = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly ServicioDeSesion _sesion;
    private readonly ServicioDeSesionDeOperador _operador;

    public ProveedorEstadoAutenticacion(ServicioDeSesion sesion, ServicioDeSesionDeOperador operador)
    {
        _sesion = sesion;
        _operador = operador;

        _sesion.Cambio += AlCambiar;
        _operador.Cambio += AlCambiar;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(Estado());

    public void Dispose()
    {
        _sesion.Cambio -= AlCambiar;
        _operador.Cambio -= AlCambiar;
    }

    private AuthenticationState Estado()
    {
        // El operador manda: si su sesión está abierta, la del inquilino no debería existir, y
        // ante la duda se muestra la del proveedor, que es la más restringida en la interfaz.
        if (_operador.Sesion is { } operador) return EstadoDeOperador(operador);

        if (_sesion.Sesion is not { } sesion) return Invitado;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, sesion.UsuarioId.ToString()),
            new(ClaimTypes.Name, sesion.Nombre),
            new(ClaimTypes.Email, sesion.Correo)
        };

        if (sesion.EmpresaActivaId is { } empresa)
            claims.Add(new Claim(ClavesDeClaim.Empresa, empresa.ToString()));

        claims.AddRange(sesion.Permisos.Select(p => new Claim(ClavesDeClaim.Permiso, p)));

        // El tipo de autenticación no es decorativo: sin él, IsAuthenticated es falso y
        // AuthorizeView trata al usuario como invitado.
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "sesion")));
    }

    /// <summary>
    /// La identidad del operador lleva su claim, sus permisos del panel y nada de tenencia,
    /// igual que su token: así una pantalla del inquilino protegida por permiso tampoco se
    /// dibuja para él.
    /// </summary>
    private static AuthenticationState EstadoDeOperador(Facturacion.Shared.Operador.SesionDeOperadorDto operador)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, operador.OperadorId.ToString()),
            new(ClaimTypes.Name, operador.Nombre),
            new(ClaimTypes.Email, operador.Correo),
            new(ClavesDeClaim.Operador, operador.OperadorId.ToString())
        };

        // Los permisos del panel viajan con la sesión y también como claims, con la misma
        // clave (ClavesDeClaim.Permiso) que en el Server: así las políticas del cliente
        // pueden enrutar y ocultar por sección.
        claims.AddRange(operador.Permisos.Select(p => new Claim(ClavesDeClaim.Permiso, p)));

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "sesion-operador")));
    }

    private void AlCambiar() => NotifyAuthenticationStateChanged(Task.FromResult(Estado()));
}
