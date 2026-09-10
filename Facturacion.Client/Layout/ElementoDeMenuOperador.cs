using Facturacion.Client.Servicios.Operador;

namespace Facturacion.Client.Layout;

/// <summary>
/// Una sección del menú del panel de operador. <see cref="Permiso"/> es la clave de permiso
/// necesaria para verla; <c>null</c> (Resumen) la ve cualquier operador activo.
/// </summary>
public sealed record ElementoDeMenuOperador(
    string Ruta,
    string Etiqueta,
    string Icono,
    string? Permiso,
    string Grupo,
    bool PrincipalEnMovil);

/// <summary>
/// Las secciones del panel del proveedor del SaaS.
///
/// <para>
/// Cada entrada guarda su permiso de lectura (<c>ver_*</c>) y el layout oculta las que el
/// operador no tiene; Resumen no lleva ninguno y siempre se muestra. Ocultar una entrada
/// aquí solo es comodidad visual, no protección: la defensa real es que el Server rechace la
/// operación.
/// </para>
/// </summary>
public static class MenuDeOperador
{
    public static class Grupos
    {
        public const string Operacion = "Operación";
        public const string Clientes = "Clientes";
        public const string Comercial = "Comercial";
        public const string Administracion = "Administración";
    }

    public static IReadOnlyList<ElementoDeMenuOperador> Elementos { get; } =
    [
        new("/operador", "Resumen", "dashboard", null, Grupos.Operacion, true),
        new("/operador/compras", "Pagos pendientes", "pending_actions", PoliticasDelPanel.VerCompras, Grupos.Operacion, true),
        new("/operador/cuentas", "Cuentas", "apartment", PoliticasDelPanel.VerClientes, Grupos.Clientes, true),
        new("/operador/usuarios", "Usuarios", "people", PoliticasDelPanel.VerUsuarios, Grupos.Clientes, false),
        new("/operador/paquetes", "Paquetes", "sell", PoliticasDelPanel.VerPaquetes, Grupos.Comercial, true),
        new("/operador/operadores", "Operadores", "supervisor_account", PoliticasDelPanel.VerOperadores, Grupos.Administracion, false),
        new("/operador/configuracion", "Configuración", "settings", PoliticasDelPanel.VerConfiguracion, Grupos.Administracion, false)
    ];
}
