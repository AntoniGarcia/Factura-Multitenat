namespace Facturacion.Shared.Operador;

public sealed record PermisoDePanel(string Clave, string Etiqueta);

public sealed record SeccionDePermisosDePanel(
    string Titulo, PermisoDePanel Ver, IReadOnlyList<PermisoDePanel> Acciones);

/// <summary>Permisos del panel del SaaS, separados de los de facturación.</summary>
public static class PermisosDePanel
{
    public const string VerPaquetes = "panel_ver_paquetes";
    public const string AdministrarPaquetes = "panel_administrar_paquetes";
    public const string VerCompras = "panel_ver_compras";
    public const string AcreditarCompras = "panel_acreditar_compras";
    public const string SoloAcreditarCompras = "panel_solo_acreditar_compras";
    public const string DescartarCompras = "panel_descartar_compras";
    public const string FacturarCompras = "panel_facturar_compras";
    public const string TimbrarCompras = "panel_timbrar_compras";
    public const string VerClientes = "panel_ver_clientes";
    public const string AdministrarClientes = "panel_administrar_clientes";
    public const string AsignarTimbres = "panel_asignar_timbres";
    public const string EditarContactoCuentas = "panel_editar_contacto_cuentas";
    public const string CambiarEstadoEmpresas = "panel_cambiar_estado_empresas";
    public const string GestionarLicencias = "panel_gestionar_licencias";
    public const string GestionarMembresias = "panel_gestionar_membresias";
    public const string GestionarLimites = "panel_gestionar_limites";
    public const string VerUsuarios = "panel_ver_usuarios";
    public const string AdministrarUsuarios = "panel_administrar_usuarios";
    public const string RestablecerContrasenasUsuarios = "panel_restablecer_contrasenas_usuarios";
    public const string CambiarCorreosUsuarios = "panel_cambiar_correos_usuarios";
    public const string CambiarEstadoUsuarios = "panel_cambiar_estado_usuarios";
    public const string QuitarAccesoUsuarios = "panel_quitar_acceso_usuarios";
    public const string VerOperadores = "panel_ver_operadores";
    public const string AdministrarOperadores = "panel_administrar_operadores";
    public const string CambiarEstadoOperadores = "panel_cambiar_estado_operadores";
    public const string RestablecerContrasenasOperadores = "panel_restablecer_contrasenas_operadores";
    public const string CambiarEstadoPaquetes = "panel_cambiar_estado_paquetes";
    public const string VerConfiguracion = "panel_ver_ajustes_sistema";
    public const string VerCatalogosSat = "panel_ver_catalogos_sat";
    public const string AdministrarConfiguracion = "panel_administrar_configuracion";
    private const string VerConfiguracionAnterior = "panel_ver_configuracion";
    public const string CrearPaquetes = "panel_crear_paquetes";
    public const string EditarPaquetes = "panel_editar_paquetes";
    public const string EliminarPaquetes = "panel_eliminar_paquetes";
    public const string RetirarPaquetes = "panel_retirar_paquetes";
    public const string ReactivarPaquetes = "panel_reactivar_paquetes";
    public const string DesactivarEmpresas = "panel_desactivar_empresas";
    public const string ReactivarEmpresas = "panel_reactivar_empresas";
    public const string CrearPaquetesExclusivos = "panel_crear_paquetes_exclusivos";
    public const string EditarPaquetesExclusivos = "panel_editar_paquetes_exclusivos";
    public const string EliminarPaquetesExclusivos = "panel_eliminar_paquetes_exclusivos";
    public const string DesactivarPaquetesExclusivos = "panel_desactivar_paquetes_exclusivos";
    public const string ReactivarPaquetesExclusivos = "panel_reactivar_paquetes_exclusivos";
    public const string DesactivarUsuarios = "panel_desactivar_usuarios";
    public const string ReactivarUsuarios = "panel_reactivar_usuarios";
    public const string CrearOperadores = "panel_crear_operadores";
    public const string EditarOperadores = "panel_editar_operadores";
    public const string EliminarOperadores = "panel_eliminar_operadores";
    public const string DesactivarOperadores = "panel_desactivar_operadores";
    public const string ReactivarOperadores = "panel_reactivar_operadores";
    public const string CambiarCorreoOperadores = "panel_cambiar_correo_operadores";
    public const string CambiarContrasenaOperadores = "panel_cambiar_contrasena_operadores";
    public const string VerActividadOperadores = "panel_ver_actividad_operadores";
    public const string LimpiarActividadOperadores = "panel_limpiar_actividad_operadores";
    public const string ActualizarCatalogosSat = "panel_actualizar_catalogos_sat";
    public const string EditarCorreoSistema = "panel_editar_correo_sistema";
    public const string ProbarCorreoSistema = "panel_probar_correo_sistema";
    public const string EditarDatosFiscalesProveedor = "panel_editar_datos_fiscales";
    public const string CargarCertificadoProveedor = "panel_cargar_certificado_proveedor";
    public const string AbrirFormularioPaquetes = "panel_politica_formulario_paquetes";

    public static IReadOnlyList<SeccionDePermisosDePanel> Secciones { get; } =
    [
        new("Paquetes", new(VerPaquetes, "Ver paquetes"),
        [
            new(CrearPaquetes, "Crear paquetes"),
            new(EditarPaquetes, "Editar paquetes"),
            new(EliminarPaquetes, "Eliminar paquetes"),
            new(RetirarPaquetes, "Retirar paquetes"),
            new(ReactivarPaquetes, "Reactivar paquetes")
        ]),
        new("Compras", new(VerCompras, "Ver compras"),
        [
            new(SoloAcreditarCompras, "Acreditar compras"),
            new(DescartarCompras, "Descartar compras"),
            new(FacturarCompras, "Preparar facturas"),
            new(TimbrarCompras, "Timbrar facturas de compras")
        ]),
        new("Cuentas", new(VerClientes, "Ver cuentas"),
        [
            new(EditarContactoCuentas, "Editar contacto de cuentas"),
            new(DesactivarEmpresas, "Desactivar empresas"),
            new(ReactivarEmpresas, "Reactivar empresas"),
            new(GestionarLicencias, "Gestionar módulos de empresas"),
            new(GestionarMembresias, "Gestionar membresías"),
            new(GestionarLimites, "Cambiar límites de la cuenta"),
            new(CrearPaquetesExclusivos, "Crear paquetes exclusivos"),
            new(EditarPaquetesExclusivos, "Editar paquetes exclusivos"),
            new(EliminarPaquetesExclusivos, "Eliminar paquetes exclusivos"),
            new(DesactivarPaquetesExclusivos, "Desactivar paquetes exclusivos"),
            new(ReactivarPaquetesExclusivos, "Reactivar paquetes exclusivos")
        ]),
        new("Usuarios", new(VerUsuarios, "Ver usuarios"),
        [
            new(RestablecerContrasenasUsuarios, "Restablecer contraseñas"),
            new(CambiarCorreosUsuarios, "Cambiar correos de usuarios"),
            new(DesactivarUsuarios, "Dar de baja a usuarios"),
            new(ReactivarUsuarios, "Reactivar usuarios"),
            new(QuitarAccesoUsuarios, "Quitar acceso a empresas")
        ]),
        new("Operadores", new(VerOperadores, "Ver operadores"),
        [
            new(CrearOperadores, "Crear operadores"),
            new(EditarOperadores, "Editar datos y permisos"),
            new(EliminarOperadores, "Eliminar operadores"),
            new(DesactivarOperadores, "Desactivar operadores"),
            new(ReactivarOperadores, "Reactivar operadores"),
            new(CambiarCorreoOperadores, "Cambiar correos de operadores"),
            new(CambiarContrasenaOperadores, "Restablecer contraseñas de operadores"),
            new(VerActividadOperadores, "Consultar actividad de operadores"),
            new(LimpiarActividadOperadores, "Limpiar historial de sesiones de operadores")
        ]),
        new("Catálogos del SAT", new(VerCatalogosSat, "Consultar catálogos del SAT"),
        [
            new(ActualizarCatalogosSat, "Actualizar catálogos del SAT")
        ]),
        new("Configuración", new(VerConfiguracion, "Ver configuración"),
        [
            new(EditarCorreoSistema, "Guardar configuración del correo y mensajes"),
            new(ProbarCorreoSistema, "Probar la conexión del correo"),
            new(EditarDatosFiscalesProveedor, "Editar los datos fiscales del proveedor"),
            new(CargarCertificadoProveedor, "Cargar el certificado del proveedor")
        ])
    ];

    public static IReadOnlyList<string> Todos { get; } =
        [.. Secciones.SelectMany(s => s.Acciones.Prepend(s.Ver)).Select(p => p.Clave)];

    private static readonly IReadOnlyDictionary<string, string> Etiquetas = Secciones
        .SelectMany(s => s.Acciones.Prepend(s.Ver)).ToDictionary(p => p.Clave, p => p.Etiqueta);

    private static readonly IReadOnlyDictionary<string, string[]> Anteriores =
        new Dictionary<string, string[]>
        {
            [CrearPaquetes] = [AdministrarPaquetes],
            [EditarPaquetes] = [AdministrarPaquetes],
            [RetirarPaquetes] = [CambiarEstadoPaquetes, AdministrarPaquetes],
            [ReactivarPaquetes] = [CambiarEstadoPaquetes, AdministrarPaquetes],
            [SoloAcreditarCompras] = [AcreditarCompras],
            [DescartarCompras] = [AcreditarCompras],
            [EditarContactoCuentas] = [AdministrarClientes],
            [DesactivarEmpresas] = [CambiarEstadoEmpresas, AdministrarClientes],
            [ReactivarEmpresas] = [CambiarEstadoEmpresas, AdministrarClientes],
            [GestionarLicencias] = [AdministrarClientes],
            [GestionarMembresias] = [AdministrarClientes],
            [GestionarLimites] = [AdministrarClientes],
            [CrearPaquetesExclusivos] = [AsignarTimbres],
            [EditarPaquetesExclusivos] = [AsignarTimbres],
            [DesactivarPaquetesExclusivos] = [AsignarTimbres],
            [ReactivarPaquetesExclusivos] = [AsignarTimbres],
            [RestablecerContrasenasUsuarios] = [AdministrarUsuarios],
            [CambiarCorreosUsuarios] = [AdministrarUsuarios],
            [DesactivarUsuarios] = [CambiarEstadoUsuarios, AdministrarUsuarios],
            [ReactivarUsuarios] = [CambiarEstadoUsuarios, AdministrarUsuarios],
            [QuitarAccesoUsuarios] = [AdministrarUsuarios],
            [CrearOperadores] = [AdministrarOperadores],
            [EditarOperadores] = [AdministrarOperadores],
            [EliminarOperadores] = [AdministrarOperadores],
            [DesactivarOperadores] = [CambiarEstadoOperadores, AdministrarOperadores],
            [ReactivarOperadores] = [CambiarEstadoOperadores, AdministrarOperadores],
            [CambiarCorreoOperadores] = [RestablecerContrasenasOperadores],
            [CambiarContrasenaOperadores] = [RestablecerContrasenasOperadores],
            [ActualizarCatalogosSat] = [AdministrarConfiguracion],
            [EditarCorreoSistema] = [AdministrarConfiguracion],
            [ProbarCorreoSistema] = [AdministrarConfiguracion],
            [EditarDatosFiscalesProveedor] = [AdministrarConfiguracion],
            [CargarCertificadoProveedor] = [AdministrarConfiguracion],
            [VerConfiguracion] = [VerConfiguracionAnterior],
            [VerCatalogosSat] = [VerConfiguracionAnterior]
        };

    private static readonly IReadOnlyDictionary<string, string> SeccionDeAccion = Secciones
        .SelectMany(s => s.Acciones.Select(a => (a.Clave, s.Ver.Clave)))
        .ToDictionary(x => x.Item1, x => x.Item2);

    public static IReadOnlyList<string> DesdeAsignacionesGuardadas(IEnumerable<string> asignados)
    {
        // La edición guarda solo casillas concretas; las claves agrupadas quedan como compatibilidad de lectura.
        var originales = asignados.ToHashSet(StringComparer.Ordinal);
        var resultado = Todos.Where(p => originales.Contains(p) ||
            Anteriores.TryGetValue(p, out var anteriores) && anteriores.Any(originales.Contains)).ToList();

        // La política anterior para actualizar catálogos exigía acceso a Configuración.
        if (!originales.Contains(ActualizarCatalogosSat) && !originales.Contains(VerConfiguracionAnterior))
            resultado.Remove(ActualizarCatalogosSat);
        return resultado;
    }

    public static bool Autoriza(IEnumerable<string>? asignados, string requerido)
    {
        if (asignados is null) return false;
        var permisos = asignados.ToHashSet(StringComparer.Ordinal);
        if (requerido == AbrirFormularioPaquetes)
            return Autoriza(permisos, CrearPaquetes) || Autoriza(permisos, EditarPaquetes);

        return permisos.Contains(requerido)
            && (!SeccionDeAccion.TryGetValue(requerido, out var ver) || permisos.Contains(ver));
    }

    public static string EtiquetaCorta(string clave) => Etiquetas.GetValueOrDefault(clave, clave);
}
