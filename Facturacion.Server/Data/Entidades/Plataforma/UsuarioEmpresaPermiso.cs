namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Un permiso concedido a un usuario en una empresa concreta. Los permisos se asignan por
/// usuario y por empresa: el mismo usuario puede ser administrador en una y auxiliar en otra.
/// <para>
/// Igual que <see cref="UsuarioEmpresa"/>, lleva <c>EmpresaId</c> pero queda fuera del filtro
/// global por la misma razón: es cableado de tenencia y hay que poder leerlo a través de
/// las empresas para armar el token.
/// </para>
/// </summary>
public sealed class UsuarioEmpresaPermiso
{
    public Guid UsuarioId { get; set; }

    public Guid EmpresaId { get; set; }

    public required string PermisoClave { get; set; }

    public UsuarioEmpresa UsuarioEmpresa { get; set; } = null!;

    public Permiso Permiso { get; set; } = null!;

    public DateTime OtorgadoUtc { get; set; }

    public Guid? OtorgadoPorUsuarioId { get; set; }
}
