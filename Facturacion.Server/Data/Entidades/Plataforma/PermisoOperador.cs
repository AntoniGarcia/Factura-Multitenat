namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Permiso asignado a un operador del SaaS.
/// <para>
/// La tabla es la intersección <c>OperadoresPlataforma x Permisos</c>. La clave primaria
/// compuesta evita duplicados: un operador no puede tener dos veces el mismo permiso.
/// </para>
/// <para>
/// Los valores de <see cref="Permiso"/> son las seis claves de <see cref="Facturacion.Shared.Comun.Permisos"/>:
/// <c>timbrar</c>, <c>cancelar</c>, <c>administrar_usuarios</c>, <c>comprar_timbres</c>,
/// <c>ver_reportes</c>, <c>configurar_empresa</c>.
/// </para>
/// </summary>
public sealed class PermisoOperador
{
    public Guid OperadorId { get; set; }
    public OperadorPlataforma Operador { get; set; } = null!;

    /// <summary>Clave del permiso (p. ej. "configurar_empresa").</summary>
    public string Permiso { get; set; } = null!;
}