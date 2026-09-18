namespace Facturacion.Shared.Operador;

/// <summary>Un permiso concreto del panel con su etiqueta para la interfaz.</summary>
public sealed record PermisoDePanel(string Clave, string Etiqueta);

/// <summary>
/// Una sección del panel: su permiso de entrada (<see cref="Ver"/>) y, donde aplica, las
/// acciones que dependen de ella (<see cref="Acciones"/>). No se dibuja una acción sin su
/// «ver»: para administrar algo primero hay que poder verlo.
/// </summary>
public sealed record SeccionDePermisosDePanel(
    string Titulo,
    PermisoDePanel Ver,
    IReadOnlyList<PermisoDePanel> Acciones);

/// <summary>
/// Los permisos del panel del operador, no los de facturación.
///
/// <para><b>Por qué son un vocabulario aparte</b></para>
/// Los permisos de <see cref="Facturacion.Shared.Comun.Permisos"/> describen lo que un usuario
/// puede hacer dentro de una empresa (timbrar, cancelar, comprar timbres…). El operador no
/// tiene empresa: lo suyo es administrar el SaaS. Mezclarlos hacía que un operador terminara
/// con permisos de inquilino que no significan nada en su interfaz.
///
/// <para><b>Una por sección: ver + acciones</b></para>
/// Cada sección se abre con su permiso <c>ver</c>; las operaciones que acepta quedan detrás de
/// él como acciones (<c>administrar</c>, <c>acreditar</c>, <c>asignar</c>). Un operador sin el
/// «ver» de una sección no entra a ella, y sin la acción no ejecuta lo que está detrás de ella.
/// El Tablero no tiene permiso: cualquier operador activo lo ve.
/// </para>
/// </summary>
public static class PermisosDePanel
{
    public const string VerPaquetes = "panel_ver_paquetes";
    public const string AdministrarPaquetes = "panel_administrar_paquetes";

    public const string VerCompras = "panel_ver_compras";
    public const string AcreditarCompras = "panel_acreditar_compras";

    public const string VerClientes = "panel_ver_clientes";
    public const string AdministrarClientes = "panel_administrar_clientes";
    public const string AsignarTimbres = "panel_asignar_timbres";

    public const string VerUsuarios = "panel_ver_usuarios";
    public const string AdministrarUsuarios = "panel_administrar_usuarios";

    public const string VerOperadores = "panel_ver_operadores";
    public const string AdministrarOperadores = "panel_administrar_operadores";

    public const string VerConfiguracion = "panel_ver_configuracion";
    public const string AdministrarConfiguracion = "panel_administrar_configuracion";

    /// <summary>Las trece claves del panel, en el orden de las secciones (ver y luego acciones).</summary>
    public static IReadOnlyList<string> Todos { get; } =
    [
        VerPaquetes,
        AdministrarPaquetes,
        VerCompras,
        AcreditarCompras,
        VerClientes,
        AdministrarClientes,
        AsignarTimbres,
        VerUsuarios,
        AdministrarUsuarios,
        VerOperadores,
        AdministrarOperadores,
        VerConfiguracion,
        AdministrarConfiguracion
    ];

    private static readonly IReadOnlyDictionary<string, string> Etiquetas = new Dictionary<string, string>
    {
        [VerPaquetes] = "Ver paquetes",
        [AdministrarPaquetes] = "Administrar paquetes",
        [VerCompras] = "Ver compras",
        [AcreditarCompras] = "Acreditar compras",
        [VerClientes] = "Ver clientes",
        [AdministrarClientes] = "Administrar clientes",
        [AsignarTimbres] = "Asignar timbres",
        [VerUsuarios] = "Ver usuarios",
        [AdministrarUsuarios] = "Administrar usuarios",
        [VerOperadores] = "Ver operadores",
        [AdministrarOperadores] = "Administrar operadores",
        [VerConfiguracion] = "Ver configuración",
        [AdministrarConfiguracion] = "Administrar configuración"
    };

    /// <summary>
    /// Las secciones con su «ver» y sus acciones, listas para dibujar el selector jerárquico.
    /// </summary>
    public static IReadOnlyList<SeccionDePermisosDePanel> Secciones { get; } =
    [
        new("Paquetes",
            new(VerPaquetes, "Ver paquetes"),
            [new(AdministrarPaquetes, "Administrar paquetes")]),
        new("Compras",
            new(VerCompras, "Ver compras"),
            [new(AcreditarCompras, "Acreditar compras")]),
        new("Clientes",
            new(VerClientes, "Ver clientes"),
            [
                new(AdministrarClientes, "Administrar clientes"),
                new(AsignarTimbres, "Asignar timbres")
            ]),
        new("Usuarios",
            new(VerUsuarios, "Ver usuarios"),
            [new(AdministrarUsuarios, "Administrar usuarios")]),
        new("Operadores",
            new(VerOperadores, "Ver operadores"),
            [new(AdministrarOperadores, "Administrar operadores")]),
        new("Configuración",
            new(VerConfiguracion, "Ver configuración"),
            [new(AdministrarConfiguracion, "Administrar configuración")])
    ];

    /// <summary>
    /// Devuelve el «ver» que una acción exige, o <c>null</c> si la clave no es una acción
    /// (o no existe). El servidor la usa para rechazar una acción sin su sección.
    /// </summary>
    public static string? VerQueExige(string clave) => clave switch
    {
        AdministrarPaquetes => VerPaquetes,
        AcreditarCompras => VerCompras,
        AdministrarClientes => VerClientes,
        AsignarTimbres => VerClientes,
        AdministrarUsuarios => VerUsuarios,
        AdministrarOperadores => VerOperadores,
        AdministrarConfiguracion => VerConfiguracion,
        _ => null
    };

    public static bool EsValido(string clave) => Etiquetas.ContainsKey(clave);

    public static string EtiquetaCorta(string clave) => Etiquetas.GetValueOrDefault(clave, clave);
}