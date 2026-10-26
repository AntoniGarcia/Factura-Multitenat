using System.Net;
using Facturacion.Client.Servicios.Documentos;
using Facturacion.Client.Servicios.Plataforma;
using Microsoft.Extensions.DependencyInjection;

namespace Facturacion.Pruebas;

public sealed class CargaDeDocumentosPruebas
{
    [Fact]
    public async Task Documento_o_pago_no_encontrado_no_lanza_excepcion()
    {
        var servicios = new ServiceCollection();
        servicios.AddHttpClient(ClientesHttp.Api, cliente => cliente.BaseAddress = new Uri("https://pruebas.local/"))
            .ConfigurePrimaryHttpMessageHandler(() => new RespuestaHttp(HttpStatusCode.NotFound));
        using var proveedor = servicios.BuildServiceProvider();
        var fabrica = proveedor.GetRequiredService<IHttpClientFactory>();
        Assert.Null(await new ServicioDeEmision(fabrica).ObtenerAsync(Guid.NewGuid()));
        Assert.Null(await new ServicioDePagos(fabrica).ObtenerAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Fallos_de_acceso_no_se_confunden_con_documentos_inexistentes()
    {
        var servicios = new ServiceCollection();
        servicios.AddHttpClient(ClientesHttp.Api, cliente => cliente.BaseAddress = new Uri("https://pruebas.local/"))
            .ConfigurePrimaryHttpMessageHandler(() => new RespuestaHttp(HttpStatusCode.Forbidden));
        using var proveedor = servicios.BuildServiceProvider();
        var fabrica = proveedor.GetRequiredService<IHttpClientFactory>();
        var factura = await Assert.ThrowsAsync<HttpRequestException>(() => new ServicioDeEmision(fabrica).ObtenerAsync(Guid.NewGuid()));
        var pago = await Assert.ThrowsAsync<HttpRequestException>(() => new ServicioDePagos(fabrica).ObtenerAsync(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Forbidden, factura.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, pago.StatusCode);
    }

    private sealed class RespuestaHttp(HttpStatusCode estado) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage peticion, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(estado));
    }
}
