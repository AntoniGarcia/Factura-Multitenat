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
    public const string SoloAcreditarCompras = "panel_solo_acreditar_compras";
    public const string DescartarCompras = "panel_descartar_compras";

    public const string VerClientes = "panel_ver_clientes";
    public const string AdministrarClientes = "panel_administrar_clientes";
    public const string AsignarTimbres = "panel_asignar_timbres";
    public const string EditarContactoCuentas = "panel_editar_contacto_cuentas";
    public const string CambiarEstadoEmpresas = "panel_cambiar_estado_empresas";
    public const string GestionarLicencias = "panel_gestionar_licencias";
    public const string GestionarMembresias = "panel_gestionar_membresias";

    public const string VerUsuarios = "panel_ver_usuarios";
    public const string AdministrarUsuarios = "panel_administrar_usuarios";
    public const string RestablecerContrasenasUsuarios = "panel_restablecer_contrasenas_usuarios";
    public const string CambiarCorreosUsuarios = "panel_cambiar_correos_usuarios";
    public const string CambiarEstadoUsuarios = "panel_cambiar_estado_usuarios";
    public const string QuitarAccesoUsuarios = "panel_quitar_acceso_usuarios";

    public const string VerOperadores = "panel_ver_operadores";
    public const string AdministrarOperadores = "panel_administrar_operadores";
    public const string CambiarEstadoOperadores = "panel_cambiar_estado_operadores";

    public const string CambiarEstadoPaquetes = "panel_cambiar_estado_paquetes";

    public const string VerConfiguracion = "panel_ver_configuracion";
    public const string AdministrarConfiguracion = "panel_administrar_configuracion";

    /// <summary>Claves del panel, en el orden de las secciones (ver y luego acciones).</summary>
    public static IReadOnlyList<string> Todos { get; } =
    [
        VerPaquetes,
        AdministrarPaquetes,
        CambiarEstadoPaquetes,
        VerCompras,
        AcreditarCompras,
        SoloAcreditarCompras,
        DescartarCompras,
        VerClientes,
        AdministrarClientes,
        AsignarTimbres,
        EditarContactoCuentas,
        CambiarEstadoEmpresas,
        GestionarLicencias,
        GestionarMembresias,
        VerUsuarios,
        AdministrarUsuarios,
        RestablecerContrasenasUsuarios,
        CambiarCorreosUsuarios,
        CambiarEstadoUsuarios,
        QuitarAccesoUsuarios,
        VerOperadores,
        AdministrarOperadores,
        CambiarEstadoOperadores,
        VerConfiguracion,
        AdministrarConfiguracion
    ];

    private static readonly IReadOnlyDictionary<string, string> Etiquetas = new Dictionary<string, string>
    {
        [VerPaquetes] = "Ver paquetes",
        [AdministrarPaquetes] = "Administrar paquetes (todas las acciones)",
        [CambiarEstadoPaquetes] = "Retirar y reactivar paquetes",
        [VerCompras] = "Ver compras",
        [AcreditarCompras] = "Resolver compras (ambas acciones)",
        [SoloAcreditarCompras] = "Solo acreditar compras",
        [DescartarCompras] = "Solo descartar compras",
        [VerClientes] = "Ver clientes",
        [AdministrarClientes] = "Administrar cuentas (todas las acciones)",
        [AsignarTimbres] = "Administrar paquetes exclusivos",
        [EditarContactoCuentas] = "Editar contacto de cuentas",
        [CambiarEstadoEmpresas] = "Desactivar y reactivar empresas",
        [GestionarLicencias] = "Gestionar módulos de empresas",
        [GestionarMembresias] = "Gestionar membresías",
        [VerUsuarios] = "Ver usuarios",
        [AdministrarUsuarios] = "Administrar usuarios (todas las acciones)",
        [RestablecerContrasenasUsuarios] = "Restablecer contraseñas",
        [CambiarCorreosUsuarios] = "Cambiar correos de usuarios",
        [CambiarEstadoUsuarios] = "Desactivar y reactivar usuarios",
        [QuitarAccesoUsuarios] = "Quitar acceso a empresas",
        [VerOperadores] = "Ver operadores",
        [AdministrarOperadores] = "Administrar operadores (todas las acciones)",
        [CambiarEstadoOperadores] = "Desactivar y reactivar operadores",
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
            [
                new(AdministrarPaquetes, Etiquetas[AdministrarPaquetes]),
                new(CambiarEstadoPaquetes, Etiquetas[CambiarEstadoPaquetes])
            ]),
        new("Compras",
            new(VerCompras, "Ver compras"),
            [
                new(AcreditarCompras, Etiquetas[AcreditarCompras]),
                new(SoloAcreditarCompras, Etiquetas[SoloAcreditarCompras]),
                new(DescartarCompras, Etiquetas[DescartarCompras])
            ]),
        new("Clientes",
            new(VerClientes, "Ver clientes"),
            [
                new(AdministrarClientes, Etiquetas[AdministrarClientes]),
                new(AsignarTimbres, Etiquetas[AsignarTimbres]),
                new(EditarContactoCuentas, Etiquetas[EditarContactoCuentas]),
                new(CambiarEstadoEmpresas, Etiquetas[CambiarEstadoEmpresas]),
                new(GestionarLicencias, Etiquetas[GestionarLicencias]),
                new(GestionarMembresias, Etiquetas[GestionarMembresias])
            ]),
        new("Usuarios",
            new(VerUsuarios, "Ver usuarios"),
            [
                new(AdministrarUsuarios, Etiquetas[AdministrarUsuarios]),
                new(RestablecerContrasenasUsuarios, Etiquetas[RestablecerContrasenasUsuarios]),
                new(CambiarCorreosUsuarios, Etiquetas[CambiarCorreosUsuarios]),
                new(CambiarEstadoUsuarios, Etiquetas[CambiarEstadoUsuarios]),
                new(QuitarAccesoUsuarios, Etiquetas[QuitarAccesoUsuarios])
            ]),
        new("Operadores",
            new(VerOperadores, "Ver operadores"),
            [
                new(AdministrarOperadores, Etiquetas[AdministrarOperadores]),
                new(CambiarEstadoOperadores, Etiquetas[CambiarEstadoOperadores])
            ]),
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
        CambiarEstadoPaquetes => VerPaquetes,
        AcreditarCompras => VerCompras,
        SoloAcreditarCompras or DescartarCompras => VerCompras,
        AdministrarClientes => VerClientes,
        AsignarTimbres => VerClientes,
        EditarContactoCuentas or CambiarEstadoEmpresas or GestionarLicencias or GestionarMembresias => VerClientes,
        AdministrarUsuarios => VerUsuarios,
        RestablecerContrasenasUsuarios or CambiarCorreosUsuarios or CambiarEstadoUsuarios or QuitarAccesoUsuarios => VerUsuarios,
        AdministrarOperadores => VerOperadores,
        CambiarEstadoOperadores => VerOperadores,
        AdministrarConfiguracion => VerConfiguracion,
        _ => null
    };

    /// <summary>Los permisos generales anteriores conservan el alcance que ya tenían.</summary>
    public static string? PermisoGeneralQueAutoriza(string clave) => clave switch
    {
        CambiarEstadoPaquetes => AdministrarPaquetes,
        SoloAcreditarCompras or DescartarCompras => AcreditarCompras,
        EditarContactoCuentas or CambiarEstadoEmpresas or GestionarLicencias or GestionarMembresias => AdministrarClientes,
        RestablecerContrasenasUsuarios or CambiarCorreosUsuarios or CambiarEstadoUsuarios or QuitarAccesoUsuarios => AdministrarUsuarios,
        CambiarEstadoOperadores => AdministrarOperadores,
        _ => null
    };

    public static bool Autoriza(IEnumerable<string>? asignados, string requerido)
    {
        if (asignados is null) return false;

        var permisos = asignados.ToHashSet(StringComparer.Ordinal);
        var ver = VerQueExige(requerido);
        if (ver is not null && !permisos.Contains(ver)) return false;

        return permisos.Contains(requerido)
            || PermisoGeneralQueAutoriza(requerido) is { } general && permisos.Contains(general);
    }

    public static bool EsValido(string clave) => Etiquetas.ContainsKey(clave);

    public static string EtiquetaCorta(string clave) => Etiquetas.GetValueOrDefault(clave, clave);
}
