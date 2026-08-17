using Facturacion.Shared.Comun;

namespace Facturacion.Client.Layout;

/// <summary>Una sección del menú lateral.</summary>
/// <param name="Permiso">
/// Clave de <c>Facturacion.Shared.Comun.Permisos</c>, o <c>null</c> si la sección es visible
/// para cualquier usuario con sesión, sin importar sus permisos en la empresa activa.
/// </param>
public sealed record ElementoDeMenu(string Ruta, string Etiqueta, string Icono, string? Permiso);

/// <summary>
/// Las secciones del sistema. Hoy solo existe el inicio: el resto de las fases agrega la
/// suya aquí, no en <c>MainLayout</c>, para que el layout no tenga que volver a tocarse
/// cada vez que aparece una pantalla nueva.
/// </summary>
public static class MenuPrincipal
{
    public static IReadOnlyList<ElementoDeMenu> Elementos { get; } =
    [
        new("/", "Inicio", "home", Permiso: null),
        new("/clientes", "Clientes", "groups", Permiso: null),
        new("/productos", "Productos", "inventory", Permiso: null),
        new("/timbres", "Timbres", "confirmation_number", Permisos.ComprarTimbres),
        new("/usuarios", "Usuarios", "people", Permisos.AdministrarUsuarios),
        new("/empresa", "Empresa", "business", Permisos.ConfigurarEmpresa),
        new("/empresa/series", "Series de folios", "tag", Permisos.ConfigurarEmpresa),
        new("/empresa/certificados", "Certificados", "verified_user", Permisos.ConfigurarEmpresa),
        new("/empresa/configuracion", "Configuración", "settings", Permisos.ConfigurarEmpresa),
        new("/admin/catalogos-sat", "Catálogos del SAT", "inventory_2", Permiso: null)
    ];
}
