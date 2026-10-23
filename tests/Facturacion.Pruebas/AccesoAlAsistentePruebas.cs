using System.Net;
using Facturacion.Server.Infra.Instalacion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Facturacion.Pruebas;

public sealed class AccesoAlAsistentePruebas
{
    [Theory]
    [InlineData("Staging", "192.168.1.25", "https://facturacion-prueba.local:7445", "same-origin", true)]
    [InlineData("Staging", "127.0.0.1", "", "none", true)]
    [InlineData("Staging", "192.168.1.25", "", "", true)]
    [InlineData("Staging", "192.168.1.25", "https://ajeno.example", "same-origin", false)]
    [InlineData("Staging", "192.168.1.25", "null", "", false)]
    [InlineData("Staging", "192.168.1.25", "", "cross-site", false)]
    [InlineData("Staging", "192.168.1.25", "https://facturacion-prueba.local:7446", "", false)]
    [InlineData("Production", "192.168.1.25", "", "", false)]
    [InlineData("Production", "127.0.0.1", "https://facturacion-prueba.local:7445", "same-origin", true)]
    [InlineData("Development", "192.168.1.25", "", "", false)]
    [InlineData("Development", "::ffff:127.0.0.1", "", "none", true)]
    public void El_asistente_restringe_el_origen_y_fuera_de_revision_solo_admite_acceso_local(
        string entorno, string ip, string origen, string sitio, bool esperado)
    {
        var constructor = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = entorno, Args = [] });
        var contexto = new DefaultHttpContext();
        contexto.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        contexto.Request.Scheme = "https";
        contexto.Request.Host = new HostString("facturacion-prueba.local:7445");
        if (origen.Length > 0) contexto.Request.Headers.Origin = origen;
        if (sitio.Length > 0) contexto.Request.Headers["Sec-Fetch-Site"] = sitio;
        contexto.Request.Headers["X-Forwarded-For"] = "127.0.0.1";
        Assert.Equal(esperado, AccesoAlAsistente.Autoriza(constructor.Environment, contexto));
    }
}
