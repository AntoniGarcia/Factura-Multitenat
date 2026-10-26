using Facturacion.Server.Infra.Correo;

namespace Facturacion.Pruebas;

public sealed class ProbadorSmtpPruebas
{
    [Theory]
    [InlineData("", 587, "usuario", "clave", "Nombre", "facturas@ejemplo.com")]
    [InlineData("https://smtp.ejemplo.com", 587, "usuario", "clave", "Nombre", "facturas@ejemplo.com")]
    [InlineData("smtp.ejemplo.com", 25, "usuario", "clave", "Nombre", "facturas@ejemplo.com")]
    [InlineData("smtp.ejemplo.com", 587, "", "clave", "Nombre", "facturas@ejemplo.com")]
    [InlineData("smtp.ejemplo.com", 587, "usuario", "", "Nombre", "facturas@ejemplo.com")]
    [InlineData("smtp.ejemplo.com", 587, "usuario\r\nBcc: otro", "clave", "Nombre", "facturas@ejemplo.com")]
    [InlineData("smtp.ejemplo.com", 587, "usuario", "clave", "Nombre\nBcc: otro", "facturas@ejemplo.com")]
    [InlineData("smtp.ejemplo.com", 587, "usuario", "clave", "Nombre", "Nombre <facturas@ejemplo.com>")]
    public void Rechaza_datos_incompletos_o_encabezados_inyectados(string servidor, int puerto,
        string usuario, string clave, string nombre, string remitente)
        => Assert.NotNull(ProbadorSmtp.Validar(servidor, puerto, usuario, clave, nombre, remitente));

    [Fact]
    public void Exige_tls_y_respeta_el_puerto_del_envio_real()
    {
        Assert.NotNull(ProbadorSmtp.Validar("smtp.ejemplo.com", 587, "usuario", "clave", "Nombre", "facturas@ejemplo.com", usarTls: false));
        Assert.NotNull(ProbadorSmtp.Validar("smtp.ejemplo.com", 465, "usuario", "clave", "Nombre", "facturas@ejemplo.com", sslDirecto: false));
        Assert.Null(ProbadorSmtp.Validar("smtp.ejemplo.com", 587, "usuario", "clave", "Nombre", "facturas@ejemplo.com", sslDirecto: false));
        Assert.Null(ProbadorSmtp.Validar("smtp.ejemplo.com", 465, "usuario", "clave", "Nombre", "facturas@ejemplo.com"));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("::1")]
    public async Task No_conecta_a_destinos_locales_o_privados(string servidor)
    {
        var resultado = await ProbadorSmtp.ProbarAsync(servidor, 587, "usuario", "clave", default);
        Assert.True(resultado.EsFallo);
        Assert.Equal("smtp-servidor-no-permitido", resultado.Error!.Codigo);
    }
}
