using System.Text.RegularExpressions;

namespace Facturacion.Shared.Comun;

// Clave de cada permiso en PascalCase (FacturaCrear = factura_crear). La usa el contrato
// IContextoEmpresa; el resto del código usa las constantes de Permisos.
public enum Permiso
{
    DocumentosListado, DocumentosDescarga, DocumentosEliminar,

    FacturaCrear, FacturaModificar, FacturaCancelar, FacturaTimbrar, FacturaEnviar, FacturaEliminar,
    NotariaCrear, NotariaModificar, NotariaCancelar, NotariaTimbrar, NotariaEnviar, NotariaEliminar,
    CartaPorteCrear, CartaPorteModificar, CartaPorteCancelar, CartaPorteTimbrar, CartaPorteEnviar, CartaPorteEliminar,
    ComercioExteriorCrear, ComercioExteriorModificar, ComercioExteriorCancelar, ComercioExteriorTimbrar,
    ComercioExteriorEnviar, ComercioExteriorEliminar,
    ObraCrear, ObraModificar, ObraCancelar, ObraTimbrar, ObraEnviar, ObraEliminar,
    PagoCrear, PagoModificar, PagoCancelar, PagoTimbrar, PagoEnviar, PagoEliminar,

    ClientesCrear, ClientesModificar, ClientesDesactivar, ClientesEliminar,
    ProductosCrear, ProductosModificar, ProductosDesactivar, ProductosEliminar,
    SeriesCrear, SeriesModificar, SeriesDesactivar, SeriesEliminar,
    VehiculosCrear, VehiculosModificar, VehiculosDesactivar, VehiculosEliminar,
    FigurasCrear, FigurasModificar, FigurasDesactivar, FigurasEliminar,

    ComprarTimbres,

    EmpresaDatos, EmpresaCertificados,
    ConfiguracionGeneral, ConfiguracionCorreo, ConfiguracionImpuestos, ConfiguracionMonedas,

    Titular
}

// Una casilla del selector: la clave que se guarda y lo que lee el titular.
public sealed record Subpermiso(string Clave, string Etiqueta);

// Un renglón de una sección (un tipo de documento o un catálogo) con sus casillas.
// Licencia: la que debe tener la empresa para ofrecerlo; nulo si no requiere ninguna.
public sealed record ElementoDePermisos(
    string Nombre, string Etiqueta, IReadOnlyList<Subpermiso> Subpermisos, string? Licencia = null);

// Una pestaña del selector; con un solo elemento sus casillas se muestran directo.
public sealed record SeccionDePermisos(string Titulo, IReadOnlyList<ElementoDePermisos> Elementos);

public static class LicenciasDePermiso
{
    public const string Notarios = "notarios";
    public const string Comercio = "comercio";
    public const string Obras = "obras";
}

/// Claves de los permisos tal como viajan en el token y se guardan en la base.
/// Titular no es una casilla: solo lo lleva quien registró la cuenta y nunca se guarda.
/// Los implícitos (ConImplicitos) tampoco se guardan; se agregan al armar el token.
public static class Permisos
{
    // ── Documentos ──────────────────────────────────────────────────────────────────────
    public const string DocumentosListado = "documentos_listado";
    public const string DocumentosDescarga = "documentos_descarga";
    public const string DocumentosEliminar = "documentos_eliminar";

    // ── Emitir: un renglón por tipo de documento ────────────────────────────────────────
    public const string FacturaCrear = "factura_crear";
    public const string FacturaModificar = "factura_modificar";
    public const string FacturaCancelar = "factura_cancelar";
    public const string FacturaTimbrar = "factura_timbrar";
    public const string FacturaEnviar = "factura_enviar";
    public const string FacturaEliminar = "factura_eliminar";

    public const string NotariaCrear = "notaria_crear";
    public const string NotariaModificar = "notaria_modificar";
    public const string NotariaCancelar = "notaria_cancelar";
    public const string NotariaTimbrar = "notaria_timbrar";
    public const string NotariaEnviar = "notaria_enviar";
    public const string NotariaEliminar = "notaria_eliminar";

    public const string CartaPorteCrear = "carta_porte_crear";
    public const string CartaPorteModificar = "carta_porte_modificar";
    public const string CartaPorteCancelar = "carta_porte_cancelar";
    public const string CartaPorteTimbrar = "carta_porte_timbrar";
    public const string CartaPorteEnviar = "carta_porte_enviar";
    public const string CartaPorteEliminar = "carta_porte_eliminar";

    public const string ComercioExteriorCrear = "comercio_exterior_crear";
    public const string ComercioExteriorModificar = "comercio_exterior_modificar";
    public const string ComercioExteriorCancelar = "comercio_exterior_cancelar";
    public const string ComercioExteriorTimbrar = "comercio_exterior_timbrar";
    public const string ComercioExteriorEnviar = "comercio_exterior_enviar";
    public const string ComercioExteriorEliminar = "comercio_exterior_eliminar";

    public const string ObraCrear = "obra_crear";
    public const string ObraModificar = "obra_modificar";
    public const string ObraCancelar = "obra_cancelar";
    public const string ObraTimbrar = "obra_timbrar";
    public const string ObraEnviar = "obra_enviar";
    public const string ObraEliminar = "obra_eliminar";

    public const string PagoCrear = "pago_crear";
    public const string PagoModificar = "pago_modificar";
    public const string PagoCancelar = "pago_cancelar";
    public const string PagoTimbrar = "pago_timbrar";
    public const string PagoEnviar = "pago_enviar";
    public const string PagoEliminar = "pago_eliminar";

    // ── Catálogos ───────────────────────────────────────────────────────────────────────
    public const string ClientesCrear = "clientes_crear";
    public const string ClientesModificar = "clientes_modificar";
    public const string ClientesDesactivar = "clientes_desactivar";
    public const string ClientesEliminar = "clientes_eliminar";

    public const string ProductosCrear = "productos_crear";
    public const string ProductosModificar = "productos_modificar";
    public const string ProductosDesactivar = "productos_desactivar";
    public const string ProductosEliminar = "productos_eliminar";

    public const string SeriesCrear = "series_crear";
    public const string SeriesModificar = "series_modificar";
    public const string SeriesDesactivar = "series_desactivar";
    public const string SeriesEliminar = "series_eliminar";

    public const string VehiculosCrear = "vehiculos_crear";
    public const string VehiculosModificar = "vehiculos_modificar";
    public const string VehiculosDesactivar = "vehiculos_desactivar";
    public const string VehiculosEliminar = "vehiculos_eliminar";

    public const string FigurasCrear = "figuras_crear";
    public const string FigurasModificar = "figuras_modificar";
    public const string FigurasDesactivar = "figuras_desactivar";
    public const string FigurasEliminar = "figuras_eliminar";

    // ── Tienda y Empresa ────────────────────────────────────────────────────────────────
    public const string ComprarTimbres = "comprar_timbres";

    public const string EmpresaDatos = "empresa_datos";
    public const string EmpresaCertificados = "empresa_certificados";

    public const string ConfiguracionGeneral = "configuracion_general";
    public const string ConfiguracionCorreo = "configuracion_correo";
    public const string ConfiguracionImpuestos = "configuracion_impuestos";
    public const string ConfiguracionMonedas = "configuracion_monedas";

    public const string Titular = "titular";

    // En el orden del selector.
    public static class Documentos
    {
        public const string Factura = "factura";
        public const string Notaria = "notaria";
        public const string CartaPorte = "carta_porte";
        public const string ComercioExterior = "comercio_exterior";
        public const string Obra = "obra";
        public const string Pago = "pago";

        public static IReadOnlyList<string> Todos { get; } = [Factura, Notaria, CartaPorte, ComercioExterior, Obra, Pago];

        // La básica y sus variantes: las que se capturan en el formulario de factura.
        public static IReadOnlyList<string> Facturas { get; } = [Factura, Notaria, ComercioExterior, Obra];
    }

    public static class Acciones
    {
        public const string Crear = "crear";
        public const string Modificar = "modificar";
        public const string Cancelar = "cancelar";
        public const string Timbrar = "timbrar";
        public const string Enviar = "enviar";
        public const string Eliminar = "eliminar";
        public const string Desactivar = "desactivar";

        // En el orden de las casillas.
        public static IReadOnlyList<string> DeDocumento { get; } = [Crear, Modificar, Cancelar, Timbrar, Enviar, Eliminar];

        public static IReadOnlyList<string> DeCatalogo { get; } = [Crear, Modificar, Desactivar, Eliminar];
    }

    public static class Catalogos
    {
        public const string Clientes = "clientes";
        public const string Productos = "productos";
        public const string Series = "series";
        public const string Vehiculos = "vehiculos";
        public const string Figuras = "figuras";
    }

    // De("pago", "cancelar") = pago_cancelar.
    public static string De(string elemento, string accion) => $"{elemento}_{accion}";

    private static readonly Dictionary<string, string> EtiquetasDeAccionDeDocumento = new()
    {
        [Acciones.Crear] = "Crear",
        [Acciones.Modificar] = "Modificar",
        [Acciones.Cancelar] = "Cancelar",
        [Acciones.Timbrar] = "Timbrar",
        [Acciones.Enviar] = "Enviar por correo",
        [Acciones.Eliminar] = "Eliminar borrador"
    };

    private static readonly Dictionary<string, string> EtiquetasDeAccionDeCatalogo = new()
    {
        [Acciones.Crear] = "Crear",
        [Acciones.Modificar] = "Modificar",
        [Acciones.Desactivar] = "Desactivar y reactivar",
        [Acciones.Eliminar] = "Eliminar"
    };

    private static ElementoDePermisos Documento(string tipo, string etiqueta, string? licencia = null) => new(
        tipo, etiqueta,
        [.. Acciones.DeDocumento.Select(a => new Subpermiso(De(tipo, a), EtiquetasDeAccionDeDocumento[a]))],
        licencia);

    private static ElementoDePermisos Catalogo(string catalogo, string etiqueta) => new(
        catalogo, etiqueta,
        [.. Acciones.DeCatalogo.Select(a => new Subpermiso(De(catalogo, a), EtiquetasDeAccionDeCatalogo[a]))]);

    // En el orden del menú.
    public static IReadOnlyList<SeccionDePermisos> Secciones { get; } =
    [
        new("Documentos",
        [
            new("documentos", "Documentos",
            [
                new(DocumentosListado, "Listado"),
                new(DocumentosDescarga, "Descarga"),
                new(DocumentosEliminar, "Eliminar borradores")
            ])
        ]),
        new("Emitir",
        [
            Documento(Documentos.Factura, "Factura básica"),
            Documento(Documentos.Notaria, "Factura con Notaría", LicenciasDePermiso.Notarios),
            Documento(Documentos.CartaPorte, "Carta Porte"),
            Documento(Documentos.ComercioExterior, "Comercio Exterior", LicenciasDePermiso.Comercio),
            Documento(Documentos.Obra, "Factura de obra", LicenciasDePermiso.Obras),
            Documento(Documentos.Pago, "Complemento de pago")
        ]),
        new("Catálogos",
        [
            Catalogo(Catalogos.Clientes, "Clientes"),
            Catalogo(Catalogos.Productos, "Productos"),
            Catalogo(Catalogos.Series, "Series y folios"),
            Catalogo(Catalogos.Vehiculos, "Vehículos"),
            Catalogo(Catalogos.Figuras, "Figuras de transporte")
        ]),
        new("Tienda",
        [
            new("tienda", "Tienda", [new(ComprarTimbres, "Timbres")])
        ]),
        new("Empresa",
        [
            new("mi_empresa", "Mi empresa",
            [
                new(EmpresaDatos, "Datos de la empresa"),
                new(EmpresaCertificados, "Certificados")
            ]),
            new("configuracion", "Configuración",
            [
                new(ConfiguracionGeneral, "General"),
                new(ConfiguracionCorreo, "Correo"),
                new(ConfiguracionImpuestos, "Impuestos"),
                new(ConfiguracionMonedas, "Monedas")
            ])
        ])
    ];

    // Fuente de la semilla de la tabla Permisos.
    public static IReadOnlyList<string> Asignables { get; } =
        [.. Secciones.SelectMany(s => s.Elementos).SelectMany(e => e.Subpermisos).Select(p => p.Clave)];

    // Las asignables más Titular.
    public static IReadOnlyList<string> Todos { get; } = [.. Asignables, Titular];

    public static IReadOnlyList<string> DeEmision { get; } =
        [.. Documentos.Todos.SelectMany(t => Acciones.DeDocumento.Select(a => De(t, a)))];

    public static IReadOnlyList<string> DelElemento(string elemento)
        => [.. Secciones.SelectMany(s => s.Elementos).Where(e => e.Nombre == elemento)
                .SelectMany(e => e.Subpermisos).Select(p => p.Clave)];

    // La misma acción en todos los tipos de documento.
    public static IReadOnlyList<string> DeAccion(string accion)
        => [.. Documentos.Todos.Select(t => De(t, accion))];

    /// Agrega los implícitos: cualquier casilla de Emitir da el listado y la descarga,
    /// la descarga da el listado, y eliminar borradores en Documentos da eliminar en
    /// todos los tipos de documento.
    public static IReadOnlyList<string> ConImplicitos(IEnumerable<string> asignados)
    {
        var resultado = new HashSet<string>(asignados);

        if (resultado.Overlaps(DeEmision))
        {
            resultado.Add(DocumentosListado);
            resultado.Add(DocumentosDescarga);
        }

        if (resultado.Contains(DocumentosDescarga))
            resultado.Add(DocumentosListado);

        if (resultado.Contains(DocumentosEliminar))
            resultado.UnionWith(DeAccion(Acciones.Eliminar));

        return [.. resultado];
    }

    // Por qué no se pueden guardar esas casillas, o nulo. No se admite la descarga sin el listado.
    public static string? ErrorDeAsignacion(IReadOnlyCollection<string> permisos)
    {
        var invalido = permisos.FirstOrDefault(p => !EsAsignable(p));

        if (invalido is not null) return $"«{invalido}» no es un permiso que se pueda asignar.";

        return permisos.Contains(DocumentosDescarga) && !permisos.Contains(DocumentosListado)
            ? "La descarga de documentos necesita también el listado."
            : null;
    }

    // Por qué una casilla no asignada queda concedida (lo que muestra el selector), o nulo.
    public static string? ConcedidoPor(string clave, IReadOnlyCollection<string> asignados)
    {
        if (asignados.Contains(clave)) return null;

        if (clave is DocumentosListado or DocumentosDescarga && asignados.Any(DeEmision.Contains))
            return "Incluido por Emitir";

        if (clave == DocumentosListado && asignados.Contains(DocumentosDescarga))
            return "Incluido por Descarga";

        if (asignados.Contains(DocumentosEliminar) && DeAccion(Acciones.Eliminar).Contains(clave))
            return "Incluido por Documentos";

        return null;
    }

    // Políticas que se cumplen con cualquiera de varios permisos; Server y Client las registran desde aquí.
    public static class Politicas
    {
        // El botón «Nuevo» y el tablero.
        public const string Crear = "politica_crear";

        // El servicio exige además la variante exacta.
        public const string EditarFacturas = "politica_editar_facturas";

        public const string EditarPagos = "politica_editar_pagos";

        public const string EditarCartaPorte = "politica_editar_carta_porte";

        // Cualquier casilla del tipo de documento: sus endpoints de consulta y captura.
        public const string Pagos = "politica_pagos";
        public const string CartaPorte = "politica_carta_porte";
        public const string Notaria = "politica_notaria";
        public const string ComercioExterior = "politica_comercio_exterior";
        public const string Obra = "politica_obra";

        // El servicio exige la del tipo exacto.
        public const string Timbrar = "politica_timbrar";
        public const string Cancelar = "politica_cancelar";
        public const string Enviar = "politica_enviar";
        public const string Eliminar = "politica_eliminar";

        // Quien emite o quien los administra.
        public const string LeerClientesYProductos = "politica_leer_clientes_productos";

        // Quien hace Carta Porte o quien los administra.
        public const string LeerTransporte = "politica_leer_transporte";

        // Quien crea o quien modifica.
        public const string EditarClientes = "politica_editar_clientes";
        public const string EditarProductos = "politica_editar_productos";

        // Cualquier casilla de series y folios.
        public const string Series = "politica_series";

        // Cualquier pestaña de Mi empresa o de Configuración.
        public const string MiEmpresa = "politica_mi_empresa";
        public const string Configuracion = "politica_configuracion";

        // Se edita desde Datos de la empresa y desde Configuración: la abre cualquiera de las dos casillas.
        public const string PerfilDeNotaria = "politica_perfil_de_notaria";

        public static IReadOnlyDictionary<string, IReadOnlyList<string>> Compuestas { get; } =
            new Dictionary<string, IReadOnlyList<string>>
            {
                [Crear] = DeAccion(Acciones.Crear),
                [EditarFacturas] =
                    [.. Documentos.Facturas.SelectMany(t => new[] { De(t, Acciones.Crear), De(t, Acciones.Modificar) })],
                [EditarPagos] = [PagoCrear, PagoModificar],
                [EditarCartaPorte] = [CartaPorteCrear, CartaPorteModificar],
                [Pagos] = DelElemento(Documentos.Pago),
                [CartaPorte] = DelElemento(Documentos.CartaPorte),
                [Notaria] = DelElemento(Documentos.Notaria),
                [ComercioExterior] = DelElemento(Documentos.ComercioExterior),
                [Obra] = DelElemento(Documentos.Obra),
                [Timbrar] = DeAccion(Acciones.Timbrar),
                [Cancelar] = DeAccion(Acciones.Cancelar),
                [Enviar] = DeAccion(Acciones.Enviar),
                [Eliminar] = DeAccion(Acciones.Eliminar),
                [LeerClientesYProductos] =
                    [.. DeEmision, .. DelElemento(Catalogos.Clientes), .. DelElemento(Catalogos.Productos)],
                [LeerTransporte] =
                    [.. DelElemento(Documentos.CartaPorte), .. DelElemento(Catalogos.Vehiculos),
                     .. DelElemento(Catalogos.Figuras)],
                [EditarClientes] = [ClientesCrear, ClientesModificar],
                [EditarProductos] = [ProductosCrear, ProductosModificar],
                [Series] = DelElemento(Catalogos.Series),
                [MiEmpresa] = [EmpresaDatos, EmpresaCertificados],
                [Configuracion] = [ConfiguracionGeneral, ConfiguracionCorreo, ConfiguracionImpuestos, ConfiguracionMonedas],
                [PerfilDeNotaria] = [EmpresaDatos, ConfiguracionGeneral]
            };
    }

    // La variante nula es la factura básica.
    public static string TipoDeVarianteDeFactura(string? variante) => variante switch
    {
        Facturacion.Shared.Documentos.VariantesDeFactura.Notaria => Documentos.Notaria,
        Facturacion.Shared.Documentos.VariantesDeFactura.ComercioExterior => Documentos.ComercioExterior,
        Facturacion.Shared.Documentos.VariantesDeFactura.Obra => Documentos.Obra,
        _ => Documentos.Factura
    };

    // Factura (con su variante), traslado o pago.
    public static string TipoDeDocumento(string tipoDeComprobante, string? variante) => tipoDeComprobante switch
    {
        "P" => Documentos.Pago,
        "T" => Documentos.CartaPorte,
        _ => TipoDeVarianteDeFactura(variante)
    };

    // Se deriva del nombre (FacturaCrear → factura_crear) para no mantener otra tabla a mano.
    public static string ACadena(this Permiso permiso)
        => Regex.Replace(permiso.ToString(), "(?<!^)([A-Z])", "_$1").ToLowerInvariant();

    private static readonly Dictionary<string, Permiso> DesdeCadenas =
        Enum.GetValues<Permiso>().ToDictionary(p => p.ACadena());

    public static Permiso Desde(string clave) => DesdeCadenas.TryGetValue(clave, out var p)
        ? p
        : throw new ArgumentOutOfRangeException(nameof(clave), clave, "Permiso desconocido.");

    public static bool EsValido(string clave) => Todos.Contains(clave);

    public static bool EsAsignable(string clave) => Asignables.Contains(clave);

    private static readonly Dictionary<string, string> Etiquetas =
        Secciones.SelectMany(s => s.Elementos)
            .SelectMany(e => e.Subpermisos.Select(p => (p.Clave,
                Etiqueta: e.Subpermisos.Count == 1 ? p.Etiqueta : $"{e.Etiqueta}: {p.Etiqueta}")))
            .Append((Clave: Titular, Etiqueta: "Titular de la cuenta"))
            .ToDictionary(x => x.Clave, x => x.Etiqueta);

    // Etiqueta para listas y mensajes: «Complemento de pago: Cancelar».
    public static string EtiquetaCorta(string clave) => Etiquetas.GetValueOrDefault(clave, clave);
}
