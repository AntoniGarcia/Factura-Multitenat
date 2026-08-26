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
    // Evita que un doble clic apile dos confirmaciones. En WASM el hilo es único, así que
    // una bandera basta: si ya hay una abierta, el segundo clic no abre nada.
    private bool _abierta;

    public async Task<bool> ConfirmarAsync(
        string titulo, string mensaje, string textoConfirmar = "Confirmar", bool esPeligrosa = false)
    {
        if (_abierta) return false;
        _abierta = true;

        try
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
        finally
        {
            _abierta = false;
        }
    }
}
