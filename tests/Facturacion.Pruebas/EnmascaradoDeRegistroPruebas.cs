using Facturacion.Server.Infra.Registro;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Facturacion.Pruebas;

/// <summary>
/// El enmascarado del registro es lo único que separa un RFC, una contraseña o una llave
/// privada de un archivo de log. Se prueba con el mismo montaje que usa la aplicación:
/// el enriquecedor y el envoltorio del destino, juntos.
/// </summary>
public sealed class EnmascaradoDeRegistroPruebas
{
    private sealed class SinkDeMemoria : ILogEventSink
    {
        public List<LogEvent> Eventos { get; } = [];

        public void Emit(LogEvent evento) => Eventos.Add(evento);
    }

    private static string Registrar(Action<ILogger> escribir)
    {
        var capturado = new SinkDeMemoria();

        using (var registro = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.With<EnriquecedorDatosSensibles>()
            .WriteTo.Enmascarado(destino => destino.Sink(capturado))
            .CreateLogger())
        {
            escribir(registro);
        }

        var evento = Assert.Single(capturado.Eventos);
        return evento.RenderMessage();
    }

    [Fact]
    public void Un_rfc_en_una_propiedad_sale_enmascarado()
    {
        var mensaje = Registrar(r => r.Information("Alta de cliente {Rfc}", "AAA010101AAA"));

        Assert.DoesNotContain("AAA010101AAA", mensaje);
        Assert.Contains("AAA**********", mensaje);
    }

    [Fact]
    public void Un_rfc_interpolado_en_el_mensaje_tambien_sale_enmascarado()
    {
        // Este es el caso que un enriquecedor no puede ver: con interpolación de C# el RFC
        // deja de ser una propiedad y queda incrustado en la plantilla del mensaje.
        var rfc = "XAXX010101000";
        var mensaje = Registrar(r => r.Information($"Timbrado del receptor {rfc}"));

        Assert.DoesNotContain("XAXX010101000", mensaje);
        Assert.Contains("XAX**********", mensaje);
    }

    [Fact]
    public void Una_contrasena_se_omite_completa_por_el_nombre_de_su_propiedad()
    {
        // Una contraseña no tiene forma reconocible: lo único que la delata es cómo se llama
        // la propiedad que la lleva.
        var mensaje = Registrar(r => r.Information("Intento de acceso {Contrasena}", "N0-Adivines-Esto"));

        Assert.DoesNotContain("N0-Adivines-Esto", mensaje);
        Assert.Contains(EnmascaradorDeTextoSensible.ValorOmitido, mensaje);
    }

    [Fact]
    public void Un_token_jwt_no_llega_al_log()
    {
        const string jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.pFrK7WQ8dQ";

        var mensaje = Registrar(r => r.Information($"Se emitió {jwt}"));

        Assert.DoesNotContain(jwt, mensaje);
        Assert.Contains("[token omitido]", mensaje);
    }

    [Fact]
    public void El_contenido_de_un_certificado_no_llega_al_log()
    {
        const string pem = """
            -----BEGIN CERTIFICATE-----
            MIIBkTCB+wIJAKZ0Xk9tQm5tMA0GCSqGSIb3DQEBCwUAMBQxEjAQBgNVBAMMCWxv
            Y2FsaG9zdDAeFw0yNjA4MTIwMDAwMDBaFw0yNzA4MTIwMDAwMDBa
            -----END CERTIFICATE-----
            """;

        var mensaje = Registrar(r => r.Information("Carga de CSD {Contenido}", pem));

        Assert.DoesNotContain("MIIBkTCB", mensaje);
        Assert.Contains("[certificado omitido]", mensaje);
    }

    [Fact]
    public void Una_clave_del_catalogo_del_sat_no_se_toca()
    {
        // Contraejemplo deliberado: si el enmascarado ocultara ClaveProdServ o ClaveUnidad,
        // los logs no servirían para diagnosticar un rechazo del PAC.
        var mensaje = Registrar(r => r.Information(
            "Concepto con {ClaveProdServ} y {ClaveUnidad}", "01010101", "H87"));

        Assert.Contains("01010101", mensaje);
        Assert.Contains("H87", mensaje);
    }
}
