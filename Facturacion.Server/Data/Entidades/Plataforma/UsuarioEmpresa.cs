namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// A qué empresas de su cuenta accede un usuario.
/// <para>
/// Lleva <c>EmpresaId</c> pero <b>no</b> implementa <see cref="IEntidadDeEmpresa"/>, y es
/// deliberado: esto es el cableado de la tenencia, no datos de una empresa. Con el filtro
/// global puesto, <c>/auth/sesion</c> no podría listar las empresas disponibles del usuario,
/// que es precisamente lo que necesita para dibujar el selector de empresa.
/// </para>
/// </summary>
public sealed class UsuarioEmpresa
{
    public Guid UsuarioId { get; set; }

    public Usuario Usuario { get; set; } = null!;

    public Guid EmpresaId { get; set; }

    public Empresa Empresa { get; set; } = null!;

    public bool Activo { get; set; } = true;

    public DateTime FechaAltaUtc { get; set; }

    public ICollection<UsuarioEmpresaPermiso> Permisos { get; set; } = [];
}
