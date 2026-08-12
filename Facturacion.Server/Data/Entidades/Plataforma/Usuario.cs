using Microsoft.AspNetCore.Identity;

namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Usuario del sistema. Deriva de <see cref="IdentityUser{TKey}"/> con <see cref="Guid"/>
/// como clave: ASP.NET Core Identity se usa solo como almacén, como motor de hash y como
/// bloqueo por intentos. La emisión de tokens es propia (CLAUDE.md §4).
/// <para>
/// Pertenece a una cuenta y accede a una o varias de sus empresas a través de
/// <see cref="UsuarioEmpresa"/>. No lleva <c>EmpresaId</c>: el mismo usuario puede ser
/// administrador en una empresa y auxiliar en otra.
/// </para>
/// </summary>
public sealed class Usuario : IdentityUser<Guid>
{
    public required string Nombre { get; set; }

    public Guid CuentaId { get; set; }

    public Cuenta Cuenta { get; set; } = null!;

    /// <summary>Baja lógica. Desactivar invalida todas sus familias de refresh token (fase 8).</summary>
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Bloqueos por intentos fallidos seguidos, sin un inicio de sesión correcto en medio.
    /// Es lo que hace creciente el castigo: Identity solo sabe bloquear por un plazo fijo.
    /// </summary>
    public int BloqueosConsecutivos { get; set; }

    public DateTime FechaAltaUtc { get; set; }

    public ICollection<UsuarioEmpresa> Empresas { get; set; } = [];
}
