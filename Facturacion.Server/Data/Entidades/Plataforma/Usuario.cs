using Microsoft.AspNetCore.Identity;

namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Usuario del sistema. Deriva de <see cref="IdentityUser{TKey}"/> con <see cref="Guid"/>
/// como clave: ASP.NET Core Identity se usa solo como almacén, como motor de hash y como
/// bloqueo por intentos. La emisión de tokens es propia.
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

    /// <summary>Desactivar el usuario revoca sus familias de refresh token.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Bloqueos por intentos fallidos seguidos, sin un inicio de sesión correcto en medio.
    /// Es lo que hace creciente el castigo: Identity solo sabe bloquear por un plazo fijo.
    /// </summary>
    public int BloqueosConsecutivos { get; set; }

    public DateTime FechaAltaUtc { get; set; }

    /// <summary>Tema guardado en el perfil; nulo utiliza el tema claro.</summary>
    public string? TemaPreferido { get; set; }

    /// <summary>
    /// Lo creó un administrador desde la pantalla de usuarios, en vez de registrarse él mismo.
    ///
    /// <para>
    /// Decide una sola cosa: <b>quién puede cambiar su contraseña</b>. Al usuario creado por
    /// un administrador se la fija el administrador y no la cambia por su cuenta; el que se
    /// registró recibió la suya por correo y sí puede cambiarla desde su perfil.
    /// </para>
    ///
    /// <para>
    /// Tiene un costo que conviene tener presente: como el administrador conoce esa
    /// contraseña, la bitácora ya no distingue con certeza al usuario de quien le creó la
    /// cuenta. Fue una decisión explícita, no un descuido.
    /// </para>
    /// </summary>
    public bool CreadoPorAdministrador { get; set; }

    /// <summary>Titular de la cuenta. Administra empresas y usuarios y recibe los permisos de sus empresas sin asignarlos individualmente.</summary>
    public bool EsTitular { get; set; }

    public ICollection<UsuarioEmpresa> Empresas { get; set; } = [];
}
