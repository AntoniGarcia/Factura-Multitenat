using MudBlazor;

namespace Facturacion.Client.Layout;

/// <summary>
/// Traduce el nombre de ícono de <see cref="ElementoDeMenu"/> al ícono de MudBlazor. Lo usan el
/// menú lateral y los selectores con tarjetas, para que una entrada se vea igual en los dos.
/// </summary>
public static class IconosDeMenu
{
    public static string Obtener(string nombre) => nombre switch
    {
        "home" => Icons.Material.Filled.Home,
        "description" => Icons.Material.Filled.Description,
        "cancel_presentation" => Icons.Material.Filled.CancelPresentation,
        "feature_play_list" => Icons.Material.Filled.FeaturedPlayList,
        "groups" => Icons.Material.Filled.Groups,
        "inventory" => Icons.Material.Filled.Inventory,
        "domain_add" => Icons.Material.Filled.DomainAdd,
        "business" => Icons.Material.Filled.Business,
        "tag" => Icons.Material.Filled.Tag,
        "verified_user" => Icons.Material.Filled.VerifiedUser,
        "settings" => Icons.Material.Filled.Settings,
        "local_shipping" => Icons.Material.Filled.LocalShipping,
        "confirmation_number" => Icons.Material.Filled.ConfirmationNumber,
        "people" => Icons.Material.Filled.People,
        "catalogo" => Icons.Material.Filled.MenuBook,
        "tienda" => Icons.Material.Filled.ShoppingCart,
        "graficas" => Icons.Material.Filled.BarChart,
        "impuestos" => Icons.Material.Filled.Percent,
        _ => Icons.Material.Filled.Circle
    };
}
