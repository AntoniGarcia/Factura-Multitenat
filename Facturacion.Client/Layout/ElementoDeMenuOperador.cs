namespace Facturacion.Client.Layout;

/// <summary>Una sección del menú del panel de operador.</summary>
public sealed record ElementoDeMenuOperador(string Ruta, string Etiqueta, string Icono);

/// <summary>
/// Las secciones del panel del proveedor del SaaS.
///
/// <para>
/// El menú muestra las secciones completas; las operaciones sensibles se guardan por permiso
/// individual del operador en el lado del Server. Ocultar una entrada aquí solo es comodidad
/// visual, no protección: la defensa real es que el Server rechace la operación.
/// </para>
/// </summary>
public static class MenuDeOperador
{
    public static IReadOnlyList<ElementoDeMenuOperador> Elementos { get; } =
    [
        new("/operador", "Tablero", "dashboard"),
        new("/operador/paquetes", "Paquetes", "sell"),
        new("/operador/compras", "Compras", "receipt_long"),
        new("/operador/cuentas", "Clientes", "apartment"),
        new("/operador/usuarios", "Usuarios", "people"),
        new("/operador/operadores", "Operadores", "supervisor_account"), 
        new("/operador/configuracion", "Configuración", "settings")
    ];
}
