using System.Globalization;
using System.Xml.Linq;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Impuestos;
using Facturacion.Server.Modules.Documentos.Salidas;

namespace Facturacion.Pruebas;

/// <summary>
/// Generación del XML de CFDI 4.0 (fase B3), sin base de datos ni archivos: el generador es
/// puro y se le entrega un comprobante ya armado.
///
/// <para><b>Lo que de verdad se está probando</b></para>
/// El riesgo que se identificó al cerrar B1: el motor de impuestos trabaja a seis decimales
/// y el XML lleva los de la moneda. Si cada cifra se redondeara por separado al escribirla,
/// la identidad <c>Total = SubTotal − Descuento + traslados − retenciones</c> se rompería
/// —<c>redondear(a) + redondear(b) ≠ redondear(a+b)</c>— y el PAC la valida sobre lo que
/// dice el XML, no sobre lo que el sistema calculó por dentro.
/// </para>
/// </summary>
public sealed class GeneracionDeXmlCfdiPruebas
{
    private static readonly XNamespace Cfdi = "http://www.sat.gob.mx/cfd/4";

    private static readonly DatosDeEmision Pesos =
        new(new DateTime(2026, 8, 17, 12, 0, 0), Decimales: 2, "30001000000400002434", "TUVCNjQ=");

    private static Comprobante Comprobante(params Concepto[] conceptos) => new()
    {
        Estatus = "borrador",
        TipoDeComprobante = "I",
        FechaEmisionUtc = new DateTime(2026, 8, 17, 18, 0, 0, DateTimeKind.Utc),
        LugarExpedicion = "42000",
        Moneda = "MXN",
        Exportacion = "01",
        MetodoPago = "PUE",
        FormaPago = "01",
        EmisorRfc = "EKU9003173C9",
        EmisorNombre = "ESCUELA KEMPER URGATE",
        EmisorRegimenFiscal = "601",
        ReceptorRfc = "XAXX010101000",
        ReceptorNombre = "PUBLICO EN GENERAL",
        ReceptorRegimenFiscal = "616",
        ReceptorDomicilioFiscal = "42000",
        ReceptorUsoCfdi = "S01",
        Conceptos = [.. conceptos]
    };

    private static Concepto Concepto(int orden, decimal importe, decimal descuento = 0, params ImpuestoConcepto[] impuestos)
        => new()
        {
            Orden = orden,
            ClaveProdServ = "01010101",
            ClaveUnidad = "H87",
            Descripcion = "SERVICIO DE PRUEBA",
            ObjetoImp = impuestos.Length == 0 ? "01" : "02",
            Cantidad = 1,
            ValorUnitario = importe,
            Importe = importe,
            Descuento = descuento,
            Impuestos = [.. impuestos]
        };

    private static ImpuestoConcepto Traslado(decimal baseGravable, decimal tasa, decimal importe) => new()
    {
        Impuesto = "002",
        TipoFactor = "Tasa",
        TasaOCuota = tasa,
        Base = baseGravable,
        Importe = importe,
        EsRetencion = false
    };

    private static ImpuestoConcepto Exento(decimal baseGravable) => new()
    {
        Impuesto = "002",
        TipoFactor = "Exento",
        TasaOCuota = null,
        Base = baseGravable,
        Importe = null,
        EsRetencion = false
    };

    private static XElement Generar(Comprobante comprobante, DatosDeEmision? datos = null)
        => new GeneradorDeXmlCfdi().Generar(comprobante, datos ?? Pesos).Root!;

    private static decimal Attr(XElement e, string nombre)
        => decimal.Parse(e.Attribute(nombre)!.Value, CultureInfo.InvariantCulture);

    // ── El caso que da nombre a la fase ─────────────────────────────────────────────────

    /// <summary>
    /// Tres renglones de 3.333333 con IVA del 16 %. A seis decimales el IVA suma 1.599999;
    /// a dos decimales, cada renglón vale 0.53 y suman 1.59. Escribir 1.60 arriba —que es lo
    /// que sale de redondear el total de seis decimales— contra 0.53 tres veces abajo es
    /// exactamente lo que el PAC rechaza.
    /// </summary>
    [Fact]
    public void Los_totales_del_xml_cuadran_con_la_suma_de_los_conceptos()
    {
        var concepto = () => Concepto(1, 3.333333m, 0, Traslado(3.333333m, 0.16m, 0.533333m));

        var raiz = Generar(Comprobante(concepto(), concepto(), concepto()));

        Assert.Equal(9.99m, Attr(raiz, "SubTotal"));

        var impuestos = raiz.Element(Cfdi + "Impuestos")!;
        Assert.Equal(1.59m, Attr(impuestos, "TotalImpuestosTrasladados"));

        // La cuenta ingenua, escrita para que se vea la diferencia.
        Assert.Equal(1.60m, Math.Round(1.599999m, 2, MotorDeImpuestos.ModoDeRedondeo));

        Assert.Equal(11.58m, Attr(raiz, "Total"));
    }

    /// <summary>
    /// La identidad que valida el PAC, comprobada sobre los atributos del XML y no sobre lo
    /// que el sistema calculó por dentro.
    /// </summary>
    [Fact]
    public void El_total_es_exactamente_subtotal_menos_descuento_mas_impuestos()
    {
        var raiz = Generar(Comprobante(
            Concepto(1, 1.111111m, 0.333333m, Traslado(0.777778m, 0.16m, 0.124444m)),
            Concepto(2, 2.222222m, 0, Traslado(2.222222m, 0.16m, 0.355556m))));

        var impuestos = raiz.Element(Cfdi + "Impuestos")!;

        var subTotal = Attr(raiz, "SubTotal");
        var descuento = Attr(raiz, "Descuento");
        var trasladados = Attr(impuestos, "TotalImpuestosTrasladados");

        Assert.Equal(subTotal - descuento + trasladados, Attr(raiz, "Total"));
    }

    /// <summary>El traslado agrupado tiene que ser la suma de los de cada renglón, ya redondeados.</summary>
    [Fact]
    public void El_traslado_agrupado_suma_los_de_cada_concepto_ya_redondeados()
    {
        var concepto = () => Concepto(1, 3.333333m, 0, Traslado(3.333333m, 0.16m, 0.533333m));
        var raiz = Generar(Comprobante(concepto(), concepto(), concepto()));

        var deConceptos = raiz.Element(Cfdi + "Conceptos")!
            .Elements(Cfdi + "Concepto")
            .Select(c => Attr(c.Element(Cfdi + "Impuestos")!.Element(Cfdi + "Traslados")!.Element(Cfdi + "Traslado")!, "Importe"))
            .Sum();

        var agrupado = Attr(
            raiz.Element(Cfdi + "Impuestos")!.Element(Cfdi + "Traslados")!.Element(Cfdi + "Traslado")!, "Importe");

        Assert.Equal(deConceptos, agrupado);
    }

    // ── Estructura que exige el XSD ─────────────────────────────────────────────────────

    [Fact]
    public void Los_hijos_van_en_el_orden_que_manda_el_esquema()
    {
        var comprobante = Comprobante(Concepto(1, 100m, 0, Traslado(100m, 0.16m, 16m)));
        comprobante.Relacionados = [new ComprobanteRelacionado
        {
            TipoRelacion = "04", UuidRelacionado = Guid.NewGuid()
        }];

        var hijos = Generar(comprobante).Elements().Select(e => e.Name.LocalName).ToArray();

        Assert.Equal(["CfdiRelacionados", "Emisor", "Receptor", "Conceptos", "Impuestos"], hijos);
    }

    /// <summary>
    /// En CFDI 4.0 <c>CfdiRelacionados</c> es <c>maxOccurs="unbounded"</c> y el tipo de
    /// relación es atributo del padre: dos tipos distintos son dos nodos, no uno con dos
    /// hijos. Se comprobó leyendo el XSD oficial, no de memoria.
    /// </summary>
    [Fact]
    public void Cada_tipo_de_relacion_produce_su_propio_nodo()
    {
        var comprobante = Comprobante(Concepto(1, 100m, 0, Traslado(100m, 0.16m, 16m)));
        comprobante.Relacionados =
        [
            new ComprobanteRelacionado { TipoRelacion = "01", UuidRelacionado = Guid.NewGuid() },
            new ComprobanteRelacionado { TipoRelacion = "01", UuidRelacionado = Guid.NewGuid() },
            new ComprobanteRelacionado { TipoRelacion = "04", UuidRelacionado = Guid.NewGuid() }
        ];

        var nodos = Generar(comprobante).Elements(Cfdi + "CfdiRelacionados").ToArray();

        Assert.Equal(2, nodos.Length);
        Assert.Equal(2, nodos.Single(n => n.Attribute("TipoRelacion")!.Value == "01").Elements().Count());
    }

    /// <summary>
    /// El XSD deja <c>TasaOCuota</c> e <c>Importe</c> opcionales en el traslado de concepto
    /// justamente para el exento. Escribirlos en cero convertiría un exento en una tasa cero,
    /// que ante el SAT es otra cosa.
    /// </summary>
    [Fact]
    public void El_exento_no_escribe_tasa_ni_importe()
    {
        var raiz = Generar(Comprobante(Concepto(1, 100m, 0, Exento(100m))));

        var traslado = raiz.Element(Cfdi + "Conceptos")!
            .Element(Cfdi + "Concepto")!
            .Element(Cfdi + "Impuestos")!
            .Element(Cfdi + "Traslados")!
            .Element(Cfdi + "Traslado")!;

        Assert.Equal("Exento", traslado.Attribute("TipoFactor")!.Value);
        Assert.Null(traslado.Attribute("TasaOCuota"));
        Assert.Null(traslado.Attribute("Importe"));
    }

    /// <summary>Un comprobante solo de exentos no lleva nodo Impuestos: no hay nada que totalizar.</summary>
    [Fact]
    public void Solo_exentos_no_produce_nodo_de_impuestos_del_comprobante()
    {
        var raiz = Generar(Comprobante(Concepto(1, 100m, 0, Exento(100m))));

        Assert.Null(raiz.Element(Cfdi + "Impuestos"));
        Assert.Equal(100m, Attr(raiz, "Total"));
    }

    [Fact]
    public void El_descuento_se_omite_cuando_es_cero()
    {
        var raiz = Generar(Comprobante(Concepto(1, 100m, 0, Traslado(100m, 0.16m, 16m))));

        Assert.Null(raiz.Attribute("Descuento"));
        Assert.Null(raiz.Element(Cfdi + "Conceptos")!.Element(Cfdi + "Concepto")!.Attribute("Descuento"));
    }

    /// <summary>
    /// La fecha del CFDI es la hora local del lugar de expedición, no UTC, y sin zona. Aquí
    /// se comprueba que se escribe tal cual la recibe, sin reconvertir.
    /// </summary>
    [Fact]
    public void La_fecha_se_escribe_en_hora_local_sin_zona()
    {
        var raiz = Generar(Comprobante(Concepto(1, 100m, 0, Traslado(100m, 0.16m, 16m))));

        Assert.Equal("2026-08-17T12:00:00", raiz.Attribute("Fecha")!.Value);
    }

    [Fact]
    public void El_sello_nace_vacio_para_poder_calcular_la_cadena_original()
    {
        var raiz = Generar(Comprobante(Concepto(1, 100m, 0, Traslado(100m, 0.16m, 16m))));

        Assert.Equal(string.Empty, raiz.Attribute("Sello")!.Value);
    }

    // ── Datos congelados ────────────────────────────────────────────────────────────────

    /// <summary>
    /// El generador no consulta cliente ni empresa: todo sale del comprobante. Es lo que
    /// hace que reimprimir una factura de hace dos años dé el mismo XML (CLAUDE.md §5).
    /// </summary>
    [Fact]
    public void Los_datos_fiscales_salen_del_comprobante_y_no_de_los_catalogos()
    {
        var comprobante = Comprobante(Concepto(1, 100m, 0, Traslado(100m, 0.16m, 16m)));
        comprobante.ReceptorNombre = "NOMBRE CONGELADO EN 2026";
        comprobante.ReceptorDomicilioFiscal = "06000";

        var receptor = Generar(comprobante).Element(Cfdi + "Receptor")!;

        Assert.Equal("NOMBRE CONGELADO EN 2026", receptor.Attribute("Nombre")!.Value);
        Assert.Equal("06000", receptor.Attribute("DomicilioFiscalReceptor")!.Value);
    }

    // ── Moneda extranjera ───────────────────────────────────────────────────────────────

    [Fact]
    public void Moneda_extranjera_escribe_el_tipo_de_cambio()
    {
        var comprobante = Comprobante(Concepto(1, 100m, 0, Traslado(100m, 0.16m, 16m)));
        comprobante.Moneda = "USD";
        comprobante.TipoCambio = 17.5m;

        var raiz = Generar(comprobante);

        Assert.Equal("USD", raiz.Attribute("Moneda")!.Value);
        Assert.Equal(17.5m, Attr(raiz, "TipoCambio"));
    }

    [Fact]
    public void Pesos_no_escribe_tipo_de_cambio()
        => Assert.Null(Generar(Comprobante(Concepto(1, 100m, 0, Traslado(100m, 0.16m, 16m)))).Attribute("TipoCambio"));
}
