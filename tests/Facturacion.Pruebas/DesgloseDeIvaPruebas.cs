using Facturacion.Server.Modules.Plataforma.Timbres;

namespace Facturacion.Pruebas;

public sealed class DesgloseDeIvaPruebas
{
    [Fact]
    public void Separa_el_iva_de_un_total_que_ya_lo_incluye()
    {
        var (subtotal, iva) = DesgloseDeIva.CalcularDesdeTotal(116m);

        Assert.Equal(100m, subtotal);
        Assert.Equal(16m, iva);
    }

    [Fact]
    public void El_desglose_conserva_el_total_a_seis_decimales()
    {
        var (subtotal, iva) = DesgloseDeIva.CalcularDesdeTotal(175m);

        Assert.Equal(150.862069m, subtotal);
        Assert.Equal(24.137931m, iva);
        Assert.Equal(175m, subtotal + iva);
    }
}
