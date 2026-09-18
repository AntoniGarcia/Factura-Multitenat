namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Permiso asignado a un operador del SaaS.
/// <para>
/// La tabla es la intersección <c>OperadoresPlataforma x Permisos</c>. La clave primaria
/// compuesta evita duplicados: un operador no puede tener dos veces el mismo permiso.
/// </para>
/// <para>
/// Los valores de <see cref="Permiso"/> son las trece claves del panel de
/// <see cref="Facturacion.Shared.Operador.PermisosDePanel"/>: <c>panel_ver_paquetes</c>,
/// <c>panel_administrar_paquetes</c>, etc. Son los permisos del proveedor del SaaS, no los
/// de facturación de los inquilinos.
/// </para>
/// </summary>
public sealed class PermisoOperador
{
    public Guid OperadorId { get; set; }
    public OperadorPlataforma Operador { get; set; } = null!;

    /// <summary>Clave del permiso (p. ej. "panel_ver_paquetes").</summary>
    public string Permiso { get; set; } = null!;
}