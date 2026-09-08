using Facturacion.Client.Servicios.Operador;

namespace Facturacion.Client.Layout;

/// <summary>
/// Una sección del menú del panel de operador. <see cref="Permiso"/> es la clave de permiso
/// necesaria para verla; <c>null</c> (el Tablero) la ve cualquier operador activo.
/// </summary>
public sealed record ElementoDeMenuOperador(string Ruta, string Etiqueta, string Icono, string? Permiso);

/// <summary>
/// Las secciones del panel del proveedor del SaaS.
///
/// <para>
/// Cada entrada guarda su permiso de lectura (<c>ver_*</c>) y el layout oculta las que el
/// operador no tiene; el Tablero no lleva ninguno y siempre se muestra. Ocultar una entrada
/// aquí solo es comodidad visual, no protección: la defensa real es que el Server rechace la
/// operación.
/// </para>
/// </summary>
public static class MenuDeOperador
{
    public static IReadOnlyList<ElementoDeMenuOperador> Elementos { get; } =
    [
        new("/operador", "Tablero", "dashboard", null),
        new("/operador/paquetes", "Paquetes", "sell", PoliticasDelPanel.VerPaquetes),
        new("/operador/compras", "Compras", "receipt_long", PoliticasDelPanel.VerCompras),
        new("/operador/cuentas", "Clientes", "apartment", PoliticasDelPanel.VerClientes),
        new("/operador/usuarios", "Usuarios", "people", PoliticasDelPanel.VerUsuarios),
        new("/operador/operadores", "Operadores", "supervisor_account", PoliticasDelPanel.VerOperadores),
        new("/operador/configuracion", "Configuración", "settings", PoliticasDelPanel.VerConfiguracion)
    ];
}