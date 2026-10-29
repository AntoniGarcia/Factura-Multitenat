using System.Net;
using System.Net.Http.Json;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Contratos;
using Microsoft.Extensions.DependencyInjection;

namespace Facturacion.Pruebas;

public sealed class MonedasDeCapturaPruebas
{
    [Fact]
    public async Task Revalida_con_etag_y_conserva_datos_ante_304_o_fallo()
    {
        using var transporte = new CatalogosCambiantesHttp();
        var servicios = new ServiceCollection();
        servicios.AddHttpClient(ClientesHttp.Api, cliente => cliente.BaseAddress = new Uri("https://pruebas.local/"))
            .ConfigurePrimaryHttpMessageHandler(() => transporte);
        using var proveedor = servicios.BuildServiceProvider();
        var catalogos = new ServicioDeCatalogos(proveedor.GetRequiredService<IHttpClientFactory>());
        await catalogos.InicializarAsync();
        Assert.Equal(1, catalogos.Revision);
        Assert.Equal(2, catalogos.ObtenerMonedasDeCaptura(["USD"]).Count);
        await catalogos.RevalidarAsync();
        Assert.Equal(1, catalogos.Revision);
        await catalogos.RevalidarAsync();
        Assert.Equal(1, catalogos.Revision);
        Assert.Equal(2, catalogos.ObtenerMonedasDeCaptura(["USD"]).Count);
        await catalogos.RevalidarAsync();
        Assert.Equal(2, catalogos.Revision);
        Assert.Equal("MXN", Assert.Single(catalogos.ObtenerMonedasDeCaptura(["USD"])).Clave);
        Assert.Equal("Peso actualizado", Assert.Single(catalogos.Obtener("c_Moneda")).Descripcion);
        await catalogos.ActualizarSiCorrespondeAsync();
        await catalogos.ActualizarSiCorrespondeAsync();
        Assert.Equal(5, transporte.Peticiones);
    }

    private sealed class CatalogosCambiantesHttp : HttpMessageHandler
    {
        public int Peticiones { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage peticion, CancellationToken ct)
        {
            var numero = ++Peticiones;
            Assert.Equal(numero == 1 ? null : numero <= 4 ? "\"uno\"" : "\"dos\"", peticion.Headers.IfNoneMatch.FirstOrDefault()?.Tag);
            if (numero is 2 or 5) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            if (numero == 3) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            IReadOnlyList<ClaveSatDto> monedas = numero == 1
                ? [new("c_Moneda", "MXN", "Peso", true), new("c_Moneda", "USD", "Dólar", true)]
                : [new("c_Moneda", "MXN", "Peso actualizado", true)];
            var respuesta = new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(new Dictionary<string, IReadOnlyList<ClaveSatDto>> { ["c_Moneda"] = monedas }) };
            respuesta.Headers.ETag = new(numero == 1 ? "\"uno\"" : "\"dos\"");
            return Task.FromResult(respuesta);
        }
    }

    [Fact]
    public async Task Solo_ofrece_el_peso_y_las_monedas_de_la_empresa_sin_recortar_catalogo_ni_otras_claves()
    {
        var datos = new Dictionary<string, IReadOnlyList<ClaveSatDto>>
        {
            ["c_Moneda"] = [new("c_Moneda", "EUR", "Euro", true), new("c_Moneda", "USD", "Dólar", true),
                new("c_Moneda", "XXX", "Sin efectos", true), new("c_Moneda", "MXN", "Peso mexicano", true)],
            ["c_FormaPago"] = [new("c_FormaPago", "03", "Transferencia", true)]
        };
        var servicios = new ServiceCollection();
        servicios.AddHttpClient(ClientesHttp.Api, cliente => cliente.BaseAddress = new Uri("https://pruebas.local/"))
            .ConfigurePrimaryHttpMessageHandler(() => new CatalogosHttp(datos));
        using var proveedor = servicios.BuildServiceProvider();
        var catalogos = new ServicioDeCatalogos(proveedor.GetRequiredService<IHttpClientFactory>());
        await catalogos.InicializarAsync();
        Assert.Equal(["MXN"], catalogos.ObtenerMonedasDeCaptura([]).Select(x => x.Clave).ToArray());
        Assert.Equal(["MXN", "EUR", "USD"], catalogos.ObtenerMonedasDeCaptura(["USD", "EUR", "MXN"]).Select(x => x.Clave).ToArray());
        Assert.Equal(4, catalogos.Obtener("c_Moneda").Count);
        Assert.Contains(catalogos.Obtener("c_Moneda"), x => x.Clave == "EUR");
        Assert.Contains(catalogos.Obtener("c_Moneda"), x => x.Clave == "XXX");
        Assert.Equal("03", Assert.Single(catalogos.Obtener("c_FormaPago")).Clave);
    }

    [Fact]
    public async Task No_inventa_opciones_si_falta_el_catalogo_o_la_moneda_no_esta_vigente()
    {
        var datos = new Dictionary<string, IReadOnlyList<ClaveSatDto>>
        {
            ["c_Moneda"] = [new("c_Moneda", "MXN", "Peso mexicano", false), new("c_Moneda", "EUR", "Euro", true)]
        };
        var servicios = new ServiceCollection();
        servicios.AddHttpClient(ClientesHttp.Api, cliente => cliente.BaseAddress = new Uri("https://pruebas.local/"))
            .ConfigurePrimaryHttpMessageHandler(() => new CatalogosHttp(datos));
        using var proveedor = servicios.BuildServiceProvider();
        var catalogos = new ServicioDeCatalogos(proveedor.GetRequiredService<IHttpClientFactory>());
        Assert.Empty(catalogos.ObtenerMonedasDeCaptura(["EUR"]));
        await catalogos.InicializarAsync();
        Assert.Equal("EUR", Assert.Single(catalogos.ObtenerMonedasDeCaptura(["EUR"])).Clave);
    }

    private sealed class CatalogosHttp(Dictionary<string, IReadOnlyList<ClaveSatDto>> datos) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage peticion, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(datos) });
    }
}
