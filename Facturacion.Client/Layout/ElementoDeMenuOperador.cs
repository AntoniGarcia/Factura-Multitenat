namespace Facturacion.Client.Layout;

/// <summary>Una sección del menú del panel de operador.</summary>
public sealed record ElementoDeMenuOperador(string Ruta, string Etiqueta, string Icono);

/// <summary>
/// Las secciones del panel del proveedor del SaaS.
///
/// <para>
/// A diferencia del menú del inquilino, aquí no hay permisos que filtren: dentro del panel el
/// operador lo ve todo. La separación de poderes de este sistema está entre el proveedor y
/// sus clientes, no dentro del proveedor; si algún día hace falta distinguir a quien mira de
/// quien acredita, se añade el permiso entonces y no antes.
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
        new("/operador/configuracion", "Configuración", "settings")
    ];
}
