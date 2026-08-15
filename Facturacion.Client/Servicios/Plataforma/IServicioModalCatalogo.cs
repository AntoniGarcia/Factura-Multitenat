using Facturacion.Client.Componentes.Comunes;
using Facturacion.Shared.Contratos;
using MudBlazor;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>Abre <see cref="ModalCatalogo"/> y devuelve la clave elegida, o <c>null</c> si se canceló.</summary>
public interface IServicioModalCatalogo
{
    Task<ClaveSatDto?> ElegirAsync(string catalogo, string titulo);
}

public sealed class ServicioModalCatalogo(IDialogService dialogos) : IServicioModalCatalogo
{
    public async Task<ClaveSatDto?> ElegirAsync(string catalogo, string titulo)
    {
        var parametros = new DialogParameters<ModalCatalogo> { { x => x.Catalogo, catalogo } };
        var opciones = new DialogOptions { CloseOnEscapeKey = true, FullWidth = true };

        var referencia = await dialogos.ShowAsync<ModalCatalogo>(titulo, parametros, opciones);
        var resultado = await referencia.Result;

        return resultado is { Canceled: false, Data: ClaveSatDto clave } ? clave : null;
    }
}
