using System.Net;
using System.Net.Http.Json;
using System.Text;
using Facturacion.Client.Servicios;
using Facturacion.Shared.Comun;

namespace Facturacion.Pruebas;

public sealed class LectorDeProblemasHttpFase4Pruebas
{
    [Fact]
    public async Task Un_401_con_cuerpo_vacio_devuelve_problema_de_sesion_caducada_sin_excepcion()
    {
        using var respuesta = new HttpResponseMessage(HttpStatusCode.Unauthorized);

        var problema = await respuesta.LeerProblemaAsync();

        Assert.Equal(401, problema.Estado);
        Assert.Equal("urn:facturacion:error:sesion-caducada", problema.Tipo);
        Assert.Contains("sesión", problema.Detalle, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Una_respuesta_html_502_de_iis_devuelve_problema_generico_sin_lanzar_json_exception()
    {
        using var respuesta = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html><body>502 Bad Gateway</body></html>", Encoding.UTF8, "text/html")
        };

        var problema = await respuesta.LeerProblemaAsync();

        Assert.Equal(502, problema.Estado);
        Assert.Equal("urn:facturacion:error:respuesta-inesperada", problema.Tipo);
    }

    [Fact]
    public async Task Un_413_de_iis_informa_que_el_archivo_es_demasiado_grande()
    {
        using var respuesta = new HttpResponseMessage(HttpStatusCode.RequestEntityTooLarge)
        {
            Content = new StringContent("<html>413.1 Request Entity Too Large</html>", Encoding.UTF8, "text/html")
        };

        var problema = await respuesta.LeerProblemaAsync();

        Assert.Equal(413, problema.Estado);
        Assert.Equal("urn:facturacion:error:archivo-demasiado-grande", problema.Tipo);
    }

    [Fact]
    public async Task Un_problem_details_valido_se_deserializa_intacto()
    {
        var original = new DetalleProblema(
            "urn:facturacion:error:sin-timbres",
            "Sin timbres",
            429,
            "No quedan timbres disponibles.",
            "/api/timbrado",
            "trace-123",
            null);

        using var respuesta = new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = JsonContent.Create(original)
        };

        var leido = await respuesta.LeerProblemaAsync();

        Assert.Equal(original.Tipo, leido.Tipo);
        Assert.Equal(original.Titulo, leido.Titulo);
        Assert.Equal(original.Estado, leido.Estado);
        Assert.Equal(original.Detalle, leido.Detalle);
    }
}
