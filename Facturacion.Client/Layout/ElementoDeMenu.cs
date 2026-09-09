using Facturacion.Shared.Comun;

namespace Facturacion.Client.Layout;

/// <summary>Una sección del menú lateral.</summary>
/// <param name="Permiso">
/// Clave de <c>Facturacion.Shared.Comun.Permisos</c>, o <c>null</c> si la sección es visible
/// para cualquier usuario con sesión, sin importar sus permisos en la empresa activa.
/// </param>
/// <param name="Grupo">
/// Encabezado bajo el que se agrupa, o <c>null</c> para el trabajo diario, que va suelto
/// arriba y sin título.
/// </param>
public sealed record ElementoDeMenu(string Ruta, string Etiqueta, string Icono, string? Permiso, string? Grupo = null);

/// <summary>
/// Las secciones del sistema. Cada fase agrega la suya aquí, no en <c>MainLayout</c>, para
/// que el layout no tenga que volver a tocarse cada vez que aparece una pantalla nueva.
///
/// <para><b>Por qué agrupado y por qué así</b></para>
/// Diez entradas planas —cuatro de ellas de configuración de empresa— obligan a leer la lista
/// entera para encontrar «Clientes». Lo que se usa a diario va suelto arriba, sin encabezado;
/// lo que se toca una vez al mes queda bajo su título.
///
/// <para>
/// Son <b>encabezados</b>, no menús desplegables anidados. Un árbol de desplegables dentro de
/// desplegables es lo que peor sobrevive al paso a barra inferior en móvil (ARQUITECTURA.md §8), y
/// obliga a dos clics para llegar a donde antes se llegaba con uno.
/// </para>
///
/// <para>
/// Aquí <b>solo</b> aparece lo que existe y funciona. Nada de entradas apagadas de módulos
/// que llegarán: un menú lleno de opciones muertas enseña al usuario a desconfiar de lo que
/// ve. Los módulos de la fase 2 —Cotizaciones, Notaría, Constructoras, Comercio Exterior,
/// Carta Porte, Addendas— entran cuando se construyan (ARQUITECTURA.md §6).
/// </para>
/// </summary>
public static class MenuPrincipal
{
    public static class Grupos
    {
        public const string Empresa = "Mi empresa";
        public const string Administracion = "Administración";
    }

    public static IReadOnlyList<ElementoDeMenu> Elementos { get; } =
    [
        // ── Diario: sin encabezado, siempre a la vista ──────────────────────────────────
        new("/", "Inicio", "home", Permiso: null),
        // Emitir no tiene entrada propia: se entra por «Documentos», que es donde se ve lo
        // que ya se emitió, y desde ahí se crea. Tener «Nueva factura» y «Documentos» como
        // hermanas obligaba a elegir entre dos puertas al mismo cuarto antes de saber
        // cuál de las dos se quería.
        new("/documentos", "Documentos", "description", Permisos.Timbrar),
        new("/clientes", "Clientes", "groups", Permiso: null),
        new("/productos", "Productos", "inventory", Permiso: null),

        // ── Mi empresa: se configura una vez y se revisa de vez en cuando ───────────────
        new("/empresa", "Datos fiscales", "business", Permisos.ConfigurarEmpresa, Grupos.Empresa),
        new("/empresa/series", "Series de folios", "tag", Permisos.ConfigurarEmpresa, Grupos.Empresa),
        new("/empresa/certificados", "Certificados", "verified_user", Permisos.ConfigurarEmpresa, Grupos.Empresa),
        new("/empresa/configuracion", "Configuración", "settings", Permisos.ConfigurarEmpresa, Grupos.Empresa),

        // ── Administración: cuenta, gente y datos del SAT ───────────────────────────────
        new("/timbres", "Timbres", "confirmation_number", Permisos.ComprarTimbres, Grupos.Administracion),
        new("/usuarios", "Usuarios", "people", Permisos.AdministrarUsuarios, Grupos.Administracion),
        // new("/admin/catalogos-sat", "Catálogos del SAT", "inventory_2", Permiso: null, Grupos.Administracion)
    ];
}
