using System.Text;
using System.Xml;
using Facturacion.Server.Modules.Documentos.Salidas;

namespace Facturacion.Pruebas;

public sealed class ComercioExteriorDelPdfPruebas
{
    private const string XmlEmitido = """
        <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" xmlns:cce20="http://www.sat.gob.mx/ComercioExterior20">
          <cfdi:Receptor ResidenciaFiscal="USA" />
          <cfdi:Conceptos>
            <cfdi:Concepto NoIdentificacion="A-10" ClaveProdServ="85176201" Descripcion="Equipo exportado" />
          </cfdi:Conceptos>
          <cfdi:Complemento>
            <cce20:ComercioExterior ClaveDePedimento="A1" CertificadoOrigen="0" TipoCambioUSD="18.5" TotalUSD="100" Incoterm="CPT">
              <cce20:Emisor><cce20:Domicilio Calle="Domicilio original" Estado="HID" Pais="MEX" CodigoPostal="42000" /></cce20:Emisor>
              <cce20:Receptor NumRegIdTrib="123456789"><cce20:Domicilio Calle="Test street" Estado="TX" Pais="USA" CodigoPostal="78701" /></cce20:Receptor>
              <cce20:Mercancias><cce20:Mercancia NoIdentificacion="A-10" FraccionArancelaria="8517620100" UnidadAduana="06" CantidadAduana="2" ValorUnitarioAduana="50" ValorDolares="100"><cce20:DescripcionesEspecificas Marca="Marca original" Modelo="M1" /></cce20:Mercancia></cce20:Mercancias>
            </cce20:ComercioExterior>
          </cfdi:Complemento>
        </cfdi:Comprobante>
        """;

    [Fact]
    public void Recupera_domicilios_e_importes_del_xml_emitido()
    {
        var datos = DatosComercioExteriorDelPdf.DesdeXml(Encoding.UTF8.GetBytes(XmlEmitido));
        Assert.NotNull(datos);
        Assert.Equal("USA", datos.ResidenciaFiscal);
        Assert.Equal("123456789", datos.NumeroRegistroTributario);
        Assert.Equal("Domicilio original", datos.DomicilioEmisor.Calle);
        Assert.Equal("CPT", datos.Incoterm);
        Assert.Equal(18.5m, datos.TipoCambioUsd);
        Assert.Equal(100m, datos.TotalUsd);
        var mercancia = Assert.Single(datos.Mercancias!);
        Assert.Equal(1, mercancia.OrdenConcepto);
        Assert.Equal("Equipo exportado", mercancia.DescripcionConcepto);
        Assert.Equal("8517620100", mercancia.FraccionArancelaria);
        Assert.Equal(2m, mercancia.CantidadAduana);
        Assert.Equal("Marca original", mercancia.Marca);
    }

    [Fact]
    public void Una_factura_sin_complemento_no_muestra_comercio_exterior()
        => Assert.Null(DatosComercioExteriorDelPdf.DesdeXml(Encoding.UTF8.GetBytes(
            "<Comprobante xmlns='http://www.sat.gob.mx/cfd/4'/>")));

    [Fact]
    public void Rechaza_mercancias_que_no_corresponden_a_los_conceptos()
        => Assert.Throws<InvalidDataException>(() => DatosComercioExteriorDelPdf.DesdeXml(
            Encoding.UTF8.GetBytes(XmlEmitido.Replace("Mercancia NoIdentificacion=\"A-10\"", "Mercancia NoIdentificacion=\"AJENO\""))));

    [Fact]
    public void No_resuelve_entidades_externas_del_xml()
        => Assert.Throws<XmlException>(() => DatosComercioExteriorDelPdf.DesdeXml(Encoding.UTF8.GetBytes(
            "<!DOCTYPE Comprobante [<!ENTITY externa SYSTEM 'file:///archivo-privado'>]><Comprobante>&externa;</Comprobante>")));
}
