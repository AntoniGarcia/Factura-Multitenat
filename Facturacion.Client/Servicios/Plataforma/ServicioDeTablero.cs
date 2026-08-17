using System.Net.Http.Json;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Client.Servicios.Plataforma;

/// <summary>
/// Llama a <c>/api/tablero</c>, que ya viene compuesto y recortado por permiso desde el
/// servidor. Una sola petición: en WebAssembly cada ida y vuelta se nota, y además evita
/// que esta pantalla tenga que decidir en el navegador qué 403 ignorar.
/// </summary>
public sealed class ServicioDeTablero(IHttpClientFactory fabrica)
{
    public async Task<TableroDto?> ObtenerAsync(CancellationToken ct = default)
        => await fabrica.CreateClient(ClientesHttp.Api).GetFromJsonAsync<TableroDto>("api/tablero", ct);
}
