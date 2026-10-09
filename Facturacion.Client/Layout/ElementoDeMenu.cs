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
/// <param name="Hijos">
/// Entradas de un submenú. Si trae hijos, la entrada no navega: solo abre y cierra la lista,
/// su <c>Ruta</c> va vacía y se ve mientras el usuario pueda ver al menos uno de los hijos.
/// </param>
/// <param name="Descripcion">
/// Frase corta para los selectores con tarjetas (el de catálogos en Inicio). El menú lateral no
/// la muestra.
/// </param>
public sealed record ElementoDeMenu(
    string Ruta,
    string Etiqueta,
    string Icono,
    string? Permiso,
    string? Grupo = null,
    IReadOnlyList<ElementoDeMenu>? Hijos = null,
    string? Descripcion = null)
{
    public static ElementoDeMenu Submenu(string etiqueta, string icono, IReadOnlyList<ElementoDeMenu> hijos, string? grupo = null)
        => new(string.Empty, etiqueta, icono, Permiso: null, grupo, hijos);
}

/// <summary>
/// Las secciones del sistema se declaran aquí para que el layout no duplique sus rutas.
///
/// <para><b>Por qué agrupado y por qué así</b></para>
/// Diez entradas planas —cuatro de ellas de configuración de empresa— obligan a leer la lista
/// entera para encontrar «Clientes». Lo que se usa a diario va suelto arriba, sin encabezado;
/// lo que se toca una vez al mes queda bajo su título.
///
/// <para>
/// Los grupos son <b>encabezados</b>. Los submenús existen, pero de un solo nivel: un árbol de
/// desplegables dentro de desplegables es lo que peor sobrevive al paso a barra inferior en
/// móvil (ARQUITECTURA.md §8). En móvil el submenú se aplana y sus hijos quedan en la barra.
/// </para>
///
/// <para>
/// Aquí <b>solo</b> aparece lo que existe y funciona. Nada de entradas apagadas: un menú
/// lleno de opciones muertas enseña al usuario a desconfiar de lo que ve.
/// </para>
/// </summary>
public static class MenuPrincipal
{
    public static class Grupos
    {
        public const string Empresa = "Mi empresa";
        public const string Administracion = "Administración";
        public const string Tienda = "Tienda";
    }

    /// <summary>
    /// Los catálogos de la empresa. Están aparte porque los leen dos lugares —el submenú lateral y
    /// el selector de catálogos de Inicio— y una ruta nueva no debe tener que agregarse dos veces.
    /// Va antes de <see cref="Elementos"/>: los inicializadores estáticos corren en orden.
    /// </summary>
    public static IReadOnlyList<ElementoDeMenu> Catalogos { get; } =
    [
        // Cada uno conserva su propio permiso: Clientes y Productos los ve quien emite o quien
        // los administra; Series, quien configura la empresa; Transporte, quien hace Carta Porte
        // o administra vehículos y figuras.
        new("/clientes", "Clientes", "groups", Permisos.Politicas.LeerClientesYProductos,
            Descripcion: "Receptores de tus comprobantes"),
        new("/productos", "Productos", "inventory", Permisos.Politicas.LeerClientesYProductos,
            Descripcion: "Precios, claves del SAT e impuestos"),
        new("/empresa/series", "Series y folios", "tag", Permisos.Configuracion,
            Descripcion: "Numeración de cada tipo de comprobante"),
        new("/empresa/transporte", "Transporte", "local_shipping", Permisos.Politicas.LeerTransporte,
            Descripcion: "Vehículos y figuras de Carta Porte"),
    ];

    public static IReadOnlyList<ElementoDeMenu> Elementos { get; } =
    [
        // ── Diario: sin encabezado, siempre a la vista ──────────────────────────────────
        new("/", "Inicio", "home", Permiso: null),
        // Emitir no tiene entrada propia: se entra por «Documentos», que es donde se ve lo
        // que ya se emitió, y desde ahí se crea. Tener «Nueva factura» y «Documentos» como
        // hermanas obligaba a elegir entre dos puertas al mismo cuarto antes de saber
        // cuál de las dos se quería.
        new("/documentos", "Documentos", "description", Permisos.Politicas.Documentos),
        // Aparte de Documentos porque es otro permiso y otro trabajo: dar seguimiento a lo que
        // sigue esperando la respuesta del receptor o del SAT (§30).
        //new("/catalogos", "Catálogos", "catalogo", Permiso: null),
        ElementoDeMenu.Submenu("Catálogos", "catalogo", Catalogos),

        // ── Mi Administración: se configura una vez y se revisa de vez en cuando ───────────────       
        new("/empresa/nueva", "Empresas", "domain_add", Permisos.Titular, Grupos.Administracion),
        new("/empresa/usuarios", "Usuarios", "people", Permisos.Titular, Grupos.Administracion),

        // ── Tienda: cuenta, gente y datos del SAT ───────────────────────────────
        new("/timbres", "Timbres", "confirmation_number", Permisos.ComprarTimbres, Grupos.Tienda),
        // new("/admin/catalogos-sat", "Catálogos del SAT", "inventory_2", Permiso: null, Grupos.Administracion)
    ];
}
