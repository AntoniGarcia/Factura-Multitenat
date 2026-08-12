using System.Security.Claims;
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
/// </summary>
public sealed class ProveedorEstadoAutenticacion : AuthenticationStateProvider, IDisposable
{
    private static readonly AuthenticationState Invitado = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly ServicioDeSesion _sesion;

    public ProveedorEstadoAutenticacion(ServicioDeSesion sesion)
    {
        _sesion = sesion;
        _sesion.Cambio += AlCambiar;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(Estado());

    public void Dispose() => _sesion.Cambio -= AlCambiar;

    private AuthenticationState Estado()
    {
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

    private void AlCambiar() => NotifyAuthenticationStateChanged(Task.FromResult(Estado()));
}
