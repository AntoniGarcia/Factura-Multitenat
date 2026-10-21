using System.Net;
using Facturacion.Server.Infra.Instalacion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Facturacion.Pruebas;

public sealed class AccesoAlAsistentePruebas
{
    [Theory]
    [InlineData("Staging", true, "127.0.0.1", "localhost:7444", "https://localhost:7444", "same-origin", true)]
    [InlineData("Staging", true, "::1", "localhost:7444", "", "none", true)]
    [InlineData("Staging", true, "::ffff:127.0.0.1", "localhost:7444", "", "", true)]
    [InlineData("Staging", true, "127.0.0.1", "127.0.0.1:7444", "", "", true)]
    [InlineData("Production", true, "127.0.0.1", "localhost:7444", "", "", false)]
    [InlineData("Development", true, "127.0.0.1", "localhost:7444", "", "", false)]
    [InlineData("Staging", false, "127.0.0.1", "localhost:7444", "", "", false)]
    [InlineData("Staging", true, "192.168.1.25", "localhost:7444", "", "", false)]
    [InlineData("Staging", true, "127.0.0.1", "ajeno.example", "", "", false)]
    [InlineData("Staging", true, "127.0.0.1", "localhost:7444", "https://ajeno.example", "", false)]
    [InlineData("Staging", true, "127.0.0.1", "localhost:7444", "null", "", false)]
    [InlineData("Staging", true, "127.0.0.1", "localhost:7444", "", "cross-site", false)]
    [InlineData("Staging", true, "127.0.0.1", "localhost:7444", "https://localhost:7443", "", false)]
    public void Solo_omite_clave_con_revision_explicita_y_origen_local(
        string entorno, bool habilitado, string ip, string host, string origen, string sitio, bool esperado)
    {
        var constructor = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = entorno, Args = [] });
        constructor.Configuration["Instalacion:PermitirSinClaveLocal"] = habilitado.ToString();
        var contexto = new DefaultHttpContext();
        contexto.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        contexto.Request.Scheme = "https";
        contexto.Request.Host = new HostString(host);
        if (origen.Length > 0) contexto.Request.Headers.Origin = origen;
        if (sitio.Length > 0) contexto.Request.Headers["Sec-Fetch-Site"] = sitio;
        contexto.Request.Headers["X-Forwarded-For"] = "127.0.0.1";
        Assert.Equal(esperado, AccesoAlAsistente.SinClaveLocal(constructor.Configuration, constructor.Environment, contexto));
    }
}
