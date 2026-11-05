using System.Xml.Linq;
using Facturacion.Server.Modules.Documentos.Salidas;

namespace Facturacion.Pruebas;

public sealed class SustitucionDePagoPruebas
{
    [Fact]
    public void Relacion_del_pago_solo_admite_un_folio_anterior_distinto_con_clave_04()
    {
        var pago = ComprobanteDeEjemplo.CrearPago();
        pago.Pagos[0].Documentos[0].Impuestos.Clear();
        pago.Pagos[0].Documentos[0].ObjetoImpDR = "01";
        var anterior = Guid.NewGuid();
        pago.Relacionados.Add(new() { TipoRelacion = "04", UuidRelacionado = anterior });
        Assert.Null(GeneradorDeXmlPago.Validar(pago));
        var xml = GeneradorDeXmlPago.Generar(pago,
            new(DateTime.Now, 2, "30001000000400002434", "TUVCNjQ=") { ZonaHoraria = TimeZoneInfo.Utc });
        XNamespace cfdi = "http://www.sat.gob.mx/cfd/4";
        var relacion = xml.Root!.Element(cfdi + "CfdiRelacionados")!;
        Assert.Equal("04", (string?)relacion.Attribute("TipoRelacion"));
        Assert.Equal(anterior.ToString().ToUpperInvariant(), (string?)relacion.Element(cfdi + "CfdiRelacionado")!.Attribute("UUID"));
        pago.Relacionados[0].TipoRelacion = "01";
        Assert.Equal("relacion-pago-invalida", GeneradorDeXmlPago.Validar(pago)?.Codigo);
        pago.Relacionados[0].TipoRelacion = "04";
        pago.Relacionados[0].UuidRelacionado = Guid.Empty;
        Assert.Equal("relacion-pago-invalida", GeneradorDeXmlPago.Validar(pago)?.Codigo);
        pago.Relacionados[0].UuidRelacionado = anterior;
        pago.Uuid = anterior;
        Assert.Equal("relacion-pago-invalida", GeneradorDeXmlPago.Validar(pago)?.Codigo);
        pago.Uuid = null;
        pago.Relacionados.Add(new() { TipoRelacion = "04", UuidRelacionado = Guid.NewGuid() });
        Assert.Equal("relacion-pago-invalida", GeneradorDeXmlPago.Validar(pago)?.Codigo);
    }
}
