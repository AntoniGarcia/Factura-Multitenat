namespace Facturacion.Server.Data.Entidades.Plataforma;

/// <summary>
/// Catálogo fijo de los seis permisos del sistema. Se siembra en la migración desde
/// <c>Facturacion.Shared.Comun.Permisos</c>, que es la única fuente de esas claves.
/// <para>
/// Existe como tabla para poder ponerle llave foránea a <see cref="UsuarioEmpresaPermiso"/>:
/// así la base rechaza un permiso inventado, no solo el código.
/// </para>
/// </summary>
public sealed class Permiso
{
    /// <summary>Clave textual, por ejemplo <c>timbrar</c>. Es la llave primaria.</summary>
    public required string Clave { get; set; }

    public required string Descripcion { get; set; }
}
