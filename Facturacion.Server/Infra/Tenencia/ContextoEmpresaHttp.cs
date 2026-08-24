using System.Security.Claims;
using Facturacion.Shared.Comun;

namespace Facturacion.Server.Infra.Tenencia;

/// <summary>
/// Lee la empresa activa, el usuario y los permisos de los claims del access token.
/// Es la única fuente de la tenencia: ningún endpoint recibe un identificador de empresa
/// por ruta, query o cuerpo (ARQUITECTURA.md §4).
/// </summary>
public sealed class ContextoEmpresaHttp(IHttpContextAccessor accesor) : IContextoEmpresaInterno
{
    private ClaimsPrincipal? Usuario => accesor.HttpContext?.User;

    public Guid? EmpresaActual => LeerGuid(ClavesDeClaim.Empresa);

    public Guid? CuentaActual => LeerGuid(ClavesDeClaim.Cuenta);

    public Guid? UsuarioActual => LeerGuid(ClavesDeClaim.Usuario);

    public bool HayEmpresa => EmpresaActual is not null;

    public Guid EmpresaId => EmpresaActual
        ?? throw new InvalidOperationException(
            "Se pidió la empresa activa en una petición que no la tiene. " +
            "Usa EmpresaActual si la operación puede ocurrir antes de elegir empresa.");

    public Guid UsuarioId => UsuarioActual
        ?? throw new InvalidOperationException("Se pidió el usuario en una petición anónima.");

    public bool Tiene(string permiso) => Usuario?.HasClaim(ClavesDeClaim.Permiso, permiso) ?? false;

    private Guid? LeerGuid(string claim)
        => Guid.TryParse(Usuario?.FindFirstValue(claim), out var valor) ? valor : null;
}
