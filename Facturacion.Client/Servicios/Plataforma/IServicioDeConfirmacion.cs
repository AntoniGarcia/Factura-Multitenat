using Facturacion.Client.Componentes.Comunes;
using MudBlazor;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>
/// Envuelve <see cref="IDialogService"/> para que pedir una confirmación sea una sola
/// línea: <c>await confirmador.ConfirmarAsync("¿Desactivar?", "El cliente dejará de...")</c>.
/// </summary>
public interface IServicioDeConfirmacion
{
    /// <summary>Devuelve <c>true</c> si el usuario confirmó, <c>false</c> si canceló o cerró el diálogo.</summary>
    Task<bool> ConfirmarAsync(
        string titulo,
        string mensaje,
        string textoConfirmar = "Confirmar",
        bool esPeligrosa = false);
}

public sealed class ServicioDeConfirmacion(IDialogService dialogos) : IServicioDeConfirmacion
{
    public async Task<bool> ConfirmarAsync(
        string titulo, string mensaje, string textoConfirmar = "Confirmar", bool esPeligrosa = false)
    {
        var parametros = new DialogParameters<ConfirmacionAccion>
        {
            { x => x.Mensaje, mensaje },
            { x => x.TextoConfirmar, textoConfirmar },
            { x => x.EsPeligrosa, esPeligrosa }
        };

        var opciones = new DialogOptions { CloseOnEscapeKey = true };

        var referencia = await dialogos.ShowAsync<ConfirmacionAccion>(titulo, parametros, opciones);
        var resultado = await referencia.Result;

        return resultado is { Canceled: false };
    }
}
