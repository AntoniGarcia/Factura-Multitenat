namespace Facturacion.Shared.Comun;

/// <summary>
/// Los permisos del sistema. La autorización es por permiso, nunca por rol (ARQUITECTURA.md §4,
/// con el cambio de §11 del 9 de octubre de 2026: permisos por sección y titular de la cuenta).
/// </summary>
public enum Permiso
{
    VerDocumentos,
    EmitirFactura,
    EmitirNotaria,
    EmitirCartaPorte,
    EmitirComercioExterior,
    EmitirObra,
    EmitirPago,
    Cancelar,
    EnviarCorreo,
    AdministrarClientes,
    AdministrarProductos,
    AdministrarVehiculos,
    AdministrarFiguras,
    ComprarTimbres,
    MiEmpresa,
    Configuracion,
    Titular
}

/// <summary>Una sección del selector de permisos: su título y sus casillas, en orden.</summary>
public sealed record SeccionDePermisos(string Titulo, IReadOnlyList<string> Claves);

/// <summary>
/// Las claves textuales de los permisos, tal como viajan en el token y como se guardan
/// en la base. Existen para que nadie escriba la cadena a mano en una política o en un claim.
///
/// <para><b>Asignables y titular</b></para>
/// Las dieciséis de <see cref="Asignables"/> son casillas que el titular reparte entre los
/// usuarios internos, por empresa. <see cref="Titular"/> no es una casilla: lo lleva en el token
/// solo el usuario que registró la cuenta, nunca se guarda en la base y es lo único que abre
/// Usuarios y el alta de empresas. Así un usuario interno no puede crear usuarios ni darse
/// permisos, por más casillas que tenga.
/// </summary>
public static class Permisos
{
    public const string VerDocumentos = "ver_documentos";

    public const string EmitirFactura = "emitir_factura";
    public const string EmitirNotaria = "emitir_notaria";
    public const string EmitirCartaPorte = "emitir_carta_porte";
    public const string EmitirComercioExterior = "emitir_comercio_exterior";
    public const string EmitirObra = "emitir_obra";
    public const string EmitirPago = "emitir_pago";
    public const string Cancelar = "cancelar";
    public const string EnviarCorreo = "enviar_correo";

    public const string AdministrarClientes = "administrar_clientes";
    public const string AdministrarProductos = "administrar_productos";
    public const string AdministrarVehiculos = "administrar_vehiculos";
    public const string AdministrarFiguras = "administrar_figuras";

    public const string ComprarTimbres = "comprar_timbres";

    public const string MiEmpresa = "mi_empresa";
    public const string Configuracion = "configuracion";

    public const string Titular = "titular";

    private static readonly Dictionary<Permiso, string> ACadenas = new()
    {
        [Permiso.VerDocumentos] = VerDocumentos,
        [Permiso.EmitirFactura] = EmitirFactura,
        [Permiso.EmitirNotaria] = EmitirNotaria,
        [Permiso.EmitirCartaPorte] = EmitirCartaPorte,
        [Permiso.EmitirComercioExterior] = EmitirComercioExterior,
        [Permiso.EmitirObra] = EmitirObra,
        [Permiso.EmitirPago] = EmitirPago,
        [Permiso.Cancelar] = Cancelar,
        [Permiso.EnviarCorreo] = EnviarCorreo,
        [Permiso.AdministrarClientes] = AdministrarClientes,
        [Permiso.AdministrarProductos] = AdministrarProductos,
        [Permiso.AdministrarVehiculos] = AdministrarVehiculos,
        [Permiso.AdministrarFiguras] = AdministrarFiguras,
        [Permiso.ComprarTimbres] = ComprarTimbres,
        [Permiso.MiEmpresa] = MiEmpresa,
        [Permiso.Configuracion] = Configuracion,
        [Permiso.Titular] = Titular
    };

    private static readonly Dictionary<string, Permiso> DesdeCadenas =
        ACadenas.ToDictionary(p => p.Value, p => p.Key);

    /// <summary>Las secciones del selector, en el orden del menú.</summary>
    public static IReadOnlyList<SeccionDePermisos> Secciones { get; } =
    [
        new("Documentos", [VerDocumentos]),
        new("Emitir",
            [EmitirFactura, EmitirNotaria, EmitirCartaPorte, EmitirComercioExterior, EmitirObra, EmitirPago,
             Cancelar, EnviarCorreo]),
        new("Catálogos", [AdministrarClientes, AdministrarProductos, AdministrarVehiculos, AdministrarFiguras]),
        new("Tienda", [ComprarTimbres]),
        new("Empresa", [MiEmpresa, Configuracion])
    ];

    /// <summary>Las casillas que se pueden asignar. Es la fuente de la semilla de la tabla Permisos.</summary>
    public static IReadOnlyList<string> Asignables { get; } = [.. Secciones.SelectMany(s => s.Claves)];

    /// <summary>Todo lo que puede viajar en el token: las asignables más <see cref="Titular"/>.</summary>
    public static IReadOnlyList<string> Todos { get; } = [.. Asignables, Titular];

    /// <summary>Los que crean un tipo de documento.</summary>
    public static IReadOnlyList<string> DeEmision { get; } =
        [EmitirFactura, EmitirNotaria, EmitirCartaPorte, EmitirComercioExterior, EmitirObra, EmitirPago];

    /// <summary>
    /// Las políticas que se cumplen con <b>cualquiera</b> de varios permisos. Server y Client las
    /// registran desde aquí para que digan lo mismo.
    /// </summary>
    public static class Politicas
    {
        /// <summary>Listado y descarga: los abre <see cref="VerDocumentos"/> o cualquier permiso de Emitir.</summary>
        public const string Documentos = "politica_documentos";

        /// <summary>Cualquier permiso que cree un documento.</summary>
        public const string Emitir = "politica_emitir";

        /// <summary>Crear o editar una factura de cualquier variante; el servicio exige la variante exacta.</summary>
        public const string EmitirFacturas = "politica_emitir_facturas";

        /// <summary>Consultar clientes y productos: quien emite o quien los administra.</summary>
        public const string LeerClientesYProductos = "politica_leer_clientes_productos";

        /// <summary>Consultar vehículos y figuras: quien hace Carta Porte o quien los administra.</summary>
        public const string LeerTransporte = "politica_leer_transporte";

        public static IReadOnlyDictionary<string, IReadOnlyList<string>> Compuestas { get; } =
            new Dictionary<string, IReadOnlyList<string>>
            {
                [Documentos] = [VerDocumentos, .. DeEmision, Cancelar, EnviarCorreo],
                [Emitir] = DeEmision,
                [EmitirFacturas] = [EmitirFactura, EmitirNotaria, EmitirComercioExterior, EmitirObra],
                [LeerClientesYProductos] = [.. DeEmision, AdministrarClientes, AdministrarProductos],
                [LeerTransporte] = [EmitirCartaPorte, AdministrarVehiculos, AdministrarFiguras]
            };
    }

    /// <summary>
    /// El permiso que exige crear o timbrar una factura de esa variante. La variante nula es la
    /// factura básica, igual que en la emisión.
    /// </summary>
    public static string ParaVarianteDeFactura(string? variante) => variante switch
    {
        Facturacion.Shared.Documentos.VariantesDeFactura.Notaria => EmitirNotaria,
        Facturacion.Shared.Documentos.VariantesDeFactura.ComercioExterior => EmitirComercioExterior,
        Facturacion.Shared.Documentos.VariantesDeFactura.Obra => EmitirObra,
        _ => EmitirFactura
    };

    public static string ACadena(this Permiso permiso) => ACadenas[permiso];

    public static Permiso Desde(string clave) => DesdeCadenas.TryGetValue(clave, out var p)
        ? p
        : throw new ArgumentOutOfRangeException(nameof(clave), clave, "Permiso desconocido.");

    public static bool EsValido(string clave) => DesdeCadenas.ContainsKey(clave);

    public static bool EsAsignable(string clave) => Asignables.Contains(clave);

    // Etiqueta corta para las casillas. La descripción larga de la tabla Permisos es para quien
    // consulta la base; esta es para quien arma el formulario y no necesita pedirle el catálogo
    // al servidor solo para dibujar casillas fijas.
    private static readonly Dictionary<string, string> Etiquetas = new()
    {
        [VerDocumentos] = "Listado y descarga",
        [EmitirFactura] = "Factura básica",
        [EmitirNotaria] = "Factura con Notaría",
        [EmitirCartaPorte] = "Carta Porte",
        [EmitirComercioExterior] = "Comercio Exterior",
        [EmitirObra] = "Factura de obra",
        [EmitirPago] = "Complemento de pago",
        [Cancelar] = "Cancelar",
        [EnviarCorreo] = "Enviar por correo",
        [AdministrarClientes] = "Clientes",
        [AdministrarProductos] = "Productos",
        [AdministrarVehiculos] = "Vehículos",
        [AdministrarFiguras] = "Figuras de transporte",
        [ComprarTimbres] = "Timbres",
        [MiEmpresa] = "Mi empresa",
        [Configuracion] = "Configuración",
        [Titular] = "Titular de la cuenta"
    };

    public static string EtiquetaCorta(string clave) => Etiquetas.GetValueOrDefault(clave, clave);
}
