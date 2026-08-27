namespace Facturacion.Client.Servicios.Operador;

/// <summary>
/// Nombre de la política del panel en el cliente. Vale lo mismo que las de permisos: sirve
/// para enrutar y para no dibujar lo que no aplica. La protección real la impone el Server.
/// </summary>
public static class PoliticasDelPanel
{
    public const string PoliticaDeOperador = "operador";
}
