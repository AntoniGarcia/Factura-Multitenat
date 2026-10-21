using Facturacion.Server.Infra.Seguridad;
using Facturacion.Server.Modules.Plataforma.Catalogos;
using Microsoft.AspNetCore.Http;

namespace Facturacion.Pruebas;

public sealed class PreparacionDeCatalogosPruebas
{
    [Fact]
    public void El_inventario_no_repite_catalogos_y_cubre_los_tres_grupos()
    {
        var inventario = PreparacionAutomaticaDeCatalogos.Inventario;
        Assert.Equal(28, inventario.Length);
        Assert.Equal(inventario.Length, inventario.Select(i => i.Catalogo).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(21, inventario.Count(i => i.Grupo == "cfdi"));
        Assert.Equal(4, inventario.Count(i => i.Grupo == "porte"));
        Assert.Equal(3, inventario.Count(i => i.Grupo == "comercio"));
        Assert.All(inventario, entrada => Assert.Equal(Path.GetFileName(entrada.Archivo), entrada.Archivo));
    }

    [Fact]
    public void La_aplicacion_se_bloquea_hasta_que_los_catalogos_esten_listos()
    {
        var estado = new EstadoDeCargaDeCatalogos();
        Assert.False(estado.Obtener().Listo);
        estado.Actualizar(new(true, "listo", "", 28, 28));
        Assert.True(estado.Obtener().Listo);
    }

    [Fact]
    public async Task La_pantalla_de_carga_no_impide_el_importmap_de_Blazor()
    {
        const string blazor = "<html><script type=\"importmap\">{\"imports\":{}}</script></html>";
        var mostrarBlazor = false;
        var middleware = new CabecerasDeSeguridad(async contexto =>
        {
            contexto.Response.ContentType = "text/html; charset=utf-8";
            await contexto.Response.WriteAsync(mostrarBlazor ? blazor : "<html>Preparando catálogos</html>");
        });
        var carga = Contexto();
        await middleware.InvokeAsync(carga);
        Assert.Equal(PoliticaDeContenido.Base, carga.Response.Headers.ContentSecurityPolicy.ToString());
        mostrarBlazor = true;
        var aplicacion = Contexto();
        await middleware.InvokeAsync(aplicacion);
        Assert.Equal(PoliticaDeContenido.ParaHtml(blazor), aplicacion.Response.Headers.ContentSecurityPolicy.ToString());
        Assert.Contains("sha256-", aplicacion.Response.Headers.ContentSecurityPolicy.ToString());
    }

    private static DefaultHttpContext Contexto()
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Path = "/operador";
        contexto.Response.Body = new MemoryStream();
        return contexto;
    }
}
