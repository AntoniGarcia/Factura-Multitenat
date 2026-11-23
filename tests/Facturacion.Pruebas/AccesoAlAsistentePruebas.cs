using System.Net;
using Facturacion.Server.Infra.Instalacion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Facturacion.Pruebas;

public sealed class AccesoAlAsistentePruebas
{
    [Theory]
    [InlineData("Staging", false, "192.168.1.25", "https://facturacion-prueba.local:7445", "same-origin", false)]
    [InlineData("Staging", false, "127.0.0.1", "", "none", true)]
    [InlineData("Staging", true, "192.168.1.25", "https://facturacion-prueba.local:7445", "same-origin", true)]
    [InlineData("Staging", true, "192.168.1.25", "", "", true)]
    [InlineData("Staging", true, "192.168.1.25", "https://ajeno.example", "same-origin", false)]
    [InlineData("Staging", true, "192.168.1.25", "null", "", false)]
    [InlineData("Staging", true, "192.168.1.25", "", "cross-site", false)]
    [InlineData("Staging", true, "192.168.1.25", "https://facturacion-prueba.local:7446", "", false)]
    [InlineData("Production", false, "192.168.1.25", "", "", false)]
    [InlineData("Production", true, "192.168.1.25", "https://facturacion-prueba.local:7445", "same-origin", false)]
    [InlineData("Production", false, "127.0.0.1", "https://facturacion-prueba.local:7445", "same-origin", true)]
    [InlineData("Development", false, "192.168.1.25", "", "", false)]
    [InlineData("Development", true, "192.168.1.25", "", "", false)]
    [InlineData("Development", false, "::ffff:127.0.0.1", "", "none", true)]
    public void El_asistente_restringe_el_origen_y_exige_bandera_explicita_para_acceso_remoto_en_revision(
        string entorno, bool permitirRemoto, string ip, string origen, string sitio, bool esperado)
    {
        var args = permitirRemoto ? new[] { $"--{AccesoAlAsistente.ClaveAccesoRemotoRevision}=true" } : Array.Empty<string>();
        var constructor = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = entorno, Args = args });
        var contexto = new DefaultHttpContext();
        contexto.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        contexto.Request.Scheme = "https";
        contexto.Request.Host = new HostString("facturacion-prueba.local:7445");
        if (origen.Length > 0) contexto.Request.Headers.Origin = origen;
        if (sitio.Length > 0) contexto.Request.Headers["Sec-Fetch-Site"] = sitio;
        contexto.Request.Headers["X-Forwarded-For"] = "127.0.0.1";
        Assert.Equal(esperado, AccesoAlAsistente.Autoriza(constructor.Environment, contexto, constructor.Configuration));
    }
}
