using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Facturacion.Client.Servicios.Plataforma;
using Facturacion.Shared.Comun;
using Microsoft.JSInterop;

namespace Facturacion.Pruebas;

public sealed class DescargasDeCatalogosPruebas
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Exportaciones_y_plantillas_usan_la_api_autenticada_y_conservan_el_archivo(bool clientes, bool plantilla)
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("Nombre,Descripción\r\nPrueba,Artículo\r\n")).ToArray();
        var respuesta = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        respuesta.Content.Headers.ContentType = new("text/csv");
        respuesta.Content.Headers.ContentDisposition = new("attachment") { FileNameStar = "catálogo.csv" };
        using var http = new ClienteDePrueba(respuesta);
        var js = new NavegadorDePrueba();
        var descargas = new ServicioDeDescargas(js, http);
        var empresa = Guid.NewGuid();

        DetalleProblema? error;
        if (clientes)
        {
            var servicio = new ServicioDeClientes(http, descargas);
            error = plantilla ? await servicio.DescargarPlantillaAsync() : await servicio.ExportarAsync(false, empresa);
        }
        else
        {
            var servicio = new ServicioDeProductos(http, descargas);
            error = plantilla ? await servicio.DescargarPlantillaAsync() : await servicio.ExportarAsync(false, empresa);
        }

        Assert.Null(error);
        Assert.Equal(ClientesHttp.Api, http.Nombre);
        Assert.Equal("Bearer", http.Autorizacion?.Scheme);
        Assert.Equal("sesion-de-prueba", http.Autorizacion?.Parameter);
        Assert.Equal($"/api/{(clientes ? "clientes" : "productos")}/{(plantilla ? "plantilla-csv" : "exportar")}", http.Ruta?.AbsolutePath);
        if (!plantilla)
        {
            Assert.Contains("activos=false", http.Ruta!.Query);
            Assert.Contains($"empresa={empresa}", http.Ruta.Query);
        }
        Assert.Equal("catálogo.csv", js.Nombre);
        Assert.Equal("text/csv", js.Tipo);
        Assert.Equal(bytes, js.Contenido);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Un_rechazo_sin_cuerpo_muestra_el_error_y_no_descarga(HttpStatusCode estado)
    {
        using var http = new ClienteDePrueba(new(estado) { Content = new ByteArrayContent([]) });
        var js = new NavegadorDePrueba();
        var error = await new ServicioDeDescargas(js, http).DescargarAsync("api/clientes/exportar", "clientes.csv");
        Assert.Equal((int)estado, error?.Estado);
        Assert.Null(js.Contenido);
    }

    [Fact]
    public async Task Conserva_el_mensaje_de_error_del_servidor_sin_generar_un_archivo()
    {
        var problema = new DetalleProblema("permiso", "Sin permiso", 403, "No puedes exportar este catálogo.", null, "", null);
        using var http = new ClienteDePrueba(new(HttpStatusCode.Forbidden) { Content = JsonContent.Create(problema) });
        var js = new NavegadorDePrueba();
        var error = await new ServicioDeDescargas(js, http).DescargarAsync("api/productos/exportar", "productos.csv");
        Assert.Equal(problema.Detalle, error?.Detalle);
        Assert.Null(js.Contenido);
    }

    private sealed class ClienteDePrueba(HttpResponseMessage respuesta) : HttpMessageHandler, IHttpClientFactory
    {
        public string? Nombre { get; private set; }
        public Uri? Ruta { get; private set; }
        public AuthenticationHeaderValue? Autorizacion { get; private set; }

        public HttpClient CreateClient(string name)
        {
            Nombre = name;
            var cliente = new HttpClient(this, disposeHandler: false) { BaseAddress = new("https://localhost/") };
            cliente.DefaultRequestHeaders.Authorization = new("Bearer", "sesion-de-prueba");
            return cliente;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Ruta = request.RequestUri;
            Autorizacion = request.Headers.Authorization;
            return Task.FromResult(respuesta);
        }
    }

    private sealed class NavegadorDePrueba : IJSRuntime, IJSObjectReference
    {
        public string? Nombre { get; private set; }
        public string? Tipo { get; private set; }
        public byte[]? Contenido { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import") return (TValue)(object)this;
            Assert.Equal("guardarDescarga", identifier);
            Nombre = (string)args![0]!;
            Tipo = (string)args[1]!;
            using var copia = new MemoryStream();
            await ((DotNetStreamReference)args[2]!).Stream.CopyToAsync(copia, cancellationToken);
            Contenido = copia.ToArray();
            return default!;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
