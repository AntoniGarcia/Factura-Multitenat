namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Un permiso concedido a un usuario en una empresa concreta. Desde el 27 de octubre de 2026
/// (AGENTS.md §11) un usuario tiene los mismos permisos en todas sus empresas; se conserva un
/// juego por empresa porque el token se arma con el de la empresa activa, y
/// <c>ServicioDeUsuarios</c> los escribe siempre iguales.
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
