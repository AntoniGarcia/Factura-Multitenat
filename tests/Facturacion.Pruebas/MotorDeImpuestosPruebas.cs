using Facturacion.Server.Modules.Documentos.Impuestos;

namespace Facturacion.Pruebas;

/// <summary>
/// El motor de impuestos de la fase B1. Es la pieza más cara de equivocar del sistema: un
/// error aquí no rompe una pantalla, emite comprobantes con importes mal calculados que el
/// SAT ya selló y que no se pueden corregir, solo cancelar y rehacer.
///
/// <para><b>Los dos casos que justifican todo lo demás</b></para>
/// <list type="number">
///   <item><description>
///     <c>Suma_por_concepto_difiere_del_impuesto_sobre_el_total</c> — la causa número uno de
///     rechazo del PAC. Si alguien «simplifica» el motor aplicando la tasa al subtotal, esa
///     prueba falla y explica por qué.
///   </description></item>
///   <item><description>
///     <c>Exento_no_lleva_importe_y_tasa_cero_si</c> — exento y tasa cero producen XML
///     distintos. Confundirlos es rechazo en el timbrado, no un detalle de presentación.
///   </description></item>
/// </list>
/// </summary>
public sealed class MotorDeImpuestosPruebas
{
    // ── Constructores cortos, para que cada prueba se lea como su caso y no como andamiaje ──

    private const string Iva = "002";
    private const string Isr = "001";

    private static ImpuestoDeConcepto Traslado(decimal tasa) =>
        new(Iva, TiposDeFactor.Tasa, tasa, EsRetencion: false);

    private static ImpuestoDeConcepto Exento() =>
        new(Iva, TiposDeFactor.Exento, null, EsRetencion: false);

    private static ImpuestoDeConcepto Retencion(string impuesto, decimal tasa) =>
        new(impuesto, TiposDeFactor.Tasa, tasa, EsRetencion: true);

    private static ConceptoACalcular Concepto(
        decimal cantidad, decimal valorUnitario, decimal descuento = 0, params ImpuestoDeConcepto[] impuestos)
        => new(cantidad, valorUnitario, descuento,
            impuestos.Length == 0 ? ObjetosDeImpuesto.NoObjeto : ObjetosDeImpuesto.SiObjeto,
            impuestos);

    private static ComprobanteACalcular Comprobante(params ConceptoACalcular[] conceptos)
        => new("MXN", null, conceptos);

    private static ComprobanteCalculado Calcular(ComprobanteACalcular comprobante)
    {
        var resultado = MotorDeImpuestos.Calcular(comprobante);
        Assert.True(resultado.EsExito, $"El cálculo falló: {resultado.Error?.Mensaje}");
        return resultado.Valor;
    }

    private static string ClaveDeError(ComprobanteACalcular comprobante)
    {
        var resultado = MotorDeImpuestos.Calcular(comprobante);
        Assert.True(resultado.EsFallo, "Se esperaba un error y el cálculo salió bien.");
        return resultado.Error!.Codigo;
    }

    // ── Importe, descuento y base ───────────────────────────────────────────────────────

    [Fact]
    public void Importe_es_cantidad_por_valor_unitario()
    {
        var r = Calcular(Comprobante(Concepto(3, 250.50m, 0, Traslado(0.16m))));

        Assert.Equal(751.500000m, r.Conceptos[0].Importe);
    }

    /// <summary>Seis decimales de cálculo: el séptimo se redondea, no se trunca.</summary>
    [Fact]
    public void Importe_se_redondea_a_seis_decimales()
    {
        // 3 × 1.111111 = 3.333333 exacto; el caso interesante es la tasa, no el producto.
        var r = Calcular(Comprobante(Concepto(3, 1.111111m, 0, Traslado(0.16m))));

        Assert.Equal(3.333333m, r.Conceptos[0].Importe);
        // 3.333333 × 0.16 = 0.53333328 → 0.533333
        Assert.Equal(0.533333m, r.Conceptos[0].Impuestos[0].Importe);
    }

    [Fact]
    public void Base_es_importe_menos_descuento()
    {
        var r = Calcular(Comprobante(Concepto(2, 50m, 10m, Traslado(0.16m))));

        Assert.Equal(100m, r.Conceptos[0].Importe);
        Assert.Equal(10m, r.Conceptos[0].Descuento);
        Assert.Equal(90m, r.Conceptos[0].Base);
        Assert.Equal(14.400000m, r.Conceptos[0].Impuestos[0].Importe);
    }

    [Fact]
    public void Descuento_igual_al_importe_deja_base_en_cero()
    {
        var r = Calcular(Comprobante(Concepto(1, 100m, 100m, Traslado(0.16m))));

        Assert.Equal(0m, r.Conceptos[0].Base);
        Assert.Equal(0m, r.Conceptos[0].Impuestos[0].Importe);
        Assert.Equal(0m, r.Total);
    }

    // ── Exento contra tasa cero ─────────────────────────────────────────────────────────

    /// <summary>
    /// La distinción entera de esta prueba: <c>null</c> contra <c>0</c>. En el XML, el exento
    /// es un nodo sin atributo de importe y la tasa cero es un nodo con importe cero.
    /// </summary>
    [Fact]
    public void Exento_no_lleva_importe_y_tasa_cero_si()
    {
        var exento = Calcular(Comprobante(Concepto(1, 100m, 0, Exento())));
        var tasaCero = Calcular(Comprobante(Concepto(1, 100m, 0, Traslado(0m))));

        Assert.Null(exento.Conceptos[0].Impuestos[0].Importe);
        Assert.Equal(0m, tasaCero.Conceptos[0].Impuestos[0].Importe);
    }

    [Fact]
    public void Exento_no_aparece_en_los_traslados_agrupados()
    {
        var r = Calcular(Comprobante(
            Concepto(1, 100m, 0, Exento()),
            Concepto(1, 100m, 0, Traslado(0.16m))));

        var traslado = Assert.Single(r.Traslados);
        Assert.Equal(100m, traslado.Base);
        Assert.Equal(16m, traslado.Importe);
    }

    [Fact]
    public void Tasa_cero_si_aparece_en_los_traslados_agrupados()
    {
        var r = Calcular(Comprobante(Concepto(1, 100m, 0, Traslado(0m))));

        var traslado = Assert.Single(r.Traslados);
        Assert.Equal(0m, traslado.Importe);
        Assert.Equal(0m, traslado.TasaOCuota);
    }

    [Fact]
    public void Exento_junto_a_gravado_suma_los_dos_al_subtotal()
    {
        var r = Calcular(Comprobante(
            Concepto(1, 100m, 0, Exento()),
            Concepto(1, 100m, 0, Traslado(0.16m))));

        Assert.Equal(200m, r.SubTotal);
        Assert.Equal(16m, r.TotalImpuestosTrasladados);
        Assert.Equal(216m, r.Total);
    }

    // ── Tasas ───────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.160000, 16)]
    [InlineData(0.080000, 8)]
    [InlineData(0.000000, 0)]
    public void Traslada_las_tasas_de_iva_del_catalogo(decimal tasa, decimal esperado)
    {
        var r = Calcular(Comprobante(Concepto(1, 100m, 0, Traslado(tasa))));

        Assert.Equal(esperado, r.TotalImpuestosTrasladados);
    }

    // ── Retenciones ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Retiene_iva_e_isr_sobre_la_base_del_concepto()
    {
        // El caso clásico de honorarios: IVA trasladado, dos terceras partes retenidas y
        // 10 % de ISR.
        var r = Calcular(Comprobante(Concepto(
            1, 100m, 0,
            Traslado(0.16m),
            Retencion(Iva, 0.106667m),
            Retencion(Isr, 0.100000m))));

        Assert.Equal(16m, r.TotalImpuestosTrasladados);
        Assert.Equal(20.666700m, r.TotalImpuestosRetenidos);
        Assert.Equal(95.333300m, r.Total);
    }

    [Fact]
    public void Retenciones_se_agrupan_por_impuesto()
    {
        var r = Calcular(Comprobante(
            Concepto(1, 100m, 0, Traslado(0.16m), Retencion(Isr, 0.10m)),
            Concepto(1, 200m, 0, Traslado(0.16m), Retencion(Isr, 0.10m))));

        var retencion = Assert.Single(r.Retenciones);
        Assert.Equal(Isr, retencion.Impuesto);
        Assert.Equal(300m, retencion.Base);
        Assert.Equal(30m, retencion.Importe);
    }

    [Fact]
    public void El_total_resta_las_retenciones()
    {
        var r = Calcular(Comprobante(Concepto(1, 1000m, 0, Retencion(Isr, 0.10m))));

        Assert.Equal(1000m, r.SubTotal);
        Assert.Equal(100m, r.TotalImpuestosRetenidos);
        Assert.Equal(900m, r.Total);
    }

    // ── Agrupación para el nodo Impuestos ───────────────────────────────────────────────

    [Fact]
    public void Misma_tasa_en_dos_conceptos_se_agrupa_en_un_renglon()
    {
        var r = Calcular(Comprobante(
            Concepto(1, 100m, 0, Traslado(0.16m)),
            Concepto(1, 200m, 0, Traslado(0.16m))));

        var traslado = Assert.Single(r.Traslados);
        Assert.Equal(300m, traslado.Base);
        Assert.Equal(48m, traslado.Importe);
    }

    [Fact]
    public void Dos_tasas_distintas_producen_dos_renglones()
    {
        var r = Calcular(Comprobante(
            Concepto(1, 100m, 0, Traslado(0.16m)),
            Concepto(1, 100m, 0, Traslado(0.08m))));

        Assert.Equal(2, r.Traslados.Count);
        Assert.Equal(24m, r.TotalImpuestosTrasladados);
    }

    // ── Totales ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Subtotal_es_antes_de_descuentos()
    {
        var r = Calcular(Comprobante(
            Concepto(1, 100m, 25m, Traslado(0.16m)),
            Concepto(1, 100m, 0, Traslado(0.16m))));

        Assert.Equal(200m, r.SubTotal);
        Assert.Equal(25m, r.Descuento);
        // Base gravada: 75 + 100 = 175 → IVA 28
        Assert.Equal(28m, r.TotalImpuestosTrasladados);
        Assert.Equal(203m, r.Total);
    }

    // ── Redondeo: los casos que dan nombre a esta fase ──────────────────────────────────

    /// <summary>
    /// El motivo por el que los totales se suman por concepto y no se calculan sobre el
    /// total. Con tres renglones de 3.333333, el IVA sumado da 1.599999 y el IVA aplicado al
    /// subtotal daría 1.600000. El PAC valida el primero; quien programe el segundo verá
    /// rechazos que no sabrá explicar.
    /// </summary>
    [Fact]
    public void Suma_por_concepto_difiere_del_impuesto_sobre_el_total()
    {
        var concepto = Concepto(3, 1.111111m, 0, Traslado(0.16m));
        var r = Calcular(Comprobante(concepto, concepto, concepto));

        Assert.Equal(9.999999m, r.SubTotal);
        Assert.Equal(1.599999m, r.TotalImpuestosTrasladados);

        // La cuenta equivocada, escrita aquí para que se vea la diferencia y no se pueda
        // argumentar que da igual.
        var sobreElTotal = Math.Round(9.999999m * 0.16m, 6, MidpointRounding.ToEven);
        Assert.Equal(1.600000m, sobreElTotal);
        Assert.NotEqual(sobreElTotal, r.TotalImpuestosTrasladados);
    }

    /// <summary>
    /// Fija el modo de redondeo en el punto medio. 0.000001 × 0.5 = 0.0000005 cae justo en
    /// la mitad: con redondeo bancario baja a 0.000000 —cero es par— y con redondeo
    /// aritmético subiría a 0.000001.
    ///
    /// <para>
    /// Los números no son realistas a propósito: hacen falta para aterrizar exactamente en el
    /// punto medio. Si algún día se confirma que el SAT pide otro modo, esta prueba falla, y
    /// eso es lo que se quiere: que el cambio sea deliberado y no un efecto colateral.
    /// </para>
    /// </summary>
    [Fact]
    public void El_punto_medio_usa_redondeo_bancario()
    {
        var r = Calcular(Comprobante(Concepto(1, 0.000001m, 0, Traslado(0.5m))));

        Assert.Equal(0.000000m, r.Conceptos[0].Impuestos[0].Importe);
        Assert.Equal(MidpointRounding.ToEven, MotorDeImpuestos.ModoDeRedondeo);
    }

    // ── Moneda extranjera ───────────────────────────────────────────────────────────────

    [Fact]
    public void Moneda_extranjera_exige_tipo_de_cambio()
        => Assert.Equal("sin-tipo-de-cambio", ClaveDeError(
            new ComprobanteACalcular("USD", null, [Concepto(1, 100m, 0, Traslado(0.16m))])));

    [Fact]
    public void Moneda_extranjera_con_tipo_de_cambio_calcula_en_su_propia_moneda()
    {
        // El tipo de cambio solo se declara: los importes no se convierten a pesos.
        var r = Calcular(new ComprobanteACalcular("USD", 17.50m, [Concepto(1, 100m, 0, Traslado(0.16m))]));

        Assert.Equal(100m, r.SubTotal);
        Assert.Equal(116m, r.Total);
    }

    [Fact]
    public void Pesos_admite_omitir_el_tipo_de_cambio()
    {
        var r = Calcular(new ComprobanteACalcular("MXN", null, [Concepto(1, 100m, 0, Traslado(0.16m))]));

        Assert.Equal(116m, r.Total);
    }

    [Fact]
    public void Pesos_con_tipo_de_cambio_distinto_de_uno_es_error()
        => Assert.Equal("tipo-de-cambio-en-pesos", ClaveDeError(
            new ComprobanteACalcular("MXN", 17.50m, [Concepto(1, 100m, 0, Traslado(0.16m))])));

    // ── Validaciones ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Exento_con_tasa_es_error()
        => Assert.Equal("exento-con-tasa", ClaveDeError(Comprobante(Concepto(
            1, 100m, 0, new ImpuestoDeConcepto(Iva, TiposDeFactor.Exento, 0m, false)))));

    [Fact]
    public void Tasa_sin_valor_es_error()
        => Assert.Equal("tasa-faltante", ClaveDeError(Comprobante(Concepto(
            1, 100m, 0, new ImpuestoDeConcepto(Iva, TiposDeFactor.Tasa, null, false)))));

    [Fact]
    public void Retencion_exenta_es_error()
        => Assert.Equal("retencion-exenta", ClaveDeError(Comprobante(Concepto(
            1, 100m, 0, new ImpuestoDeConcepto(Iva, TiposDeFactor.Exento, null, EsRetencion: true)))));

    [Fact]
    public void No_objeto_de_impuesto_con_impuestos_es_error()
        => Assert.Equal("no-objeto-con-impuestos", ClaveDeError(Comprobante(
            new ConceptoACalcular(1, 100m, 0, ObjetosDeImpuesto.NoObjeto, [Traslado(0.16m)]))));

    [Fact]
    public void Objeto_de_impuesto_sin_impuestos_es_error()
        => Assert.Equal("si-objeto-sin-impuestos", ClaveDeError(Comprobante(
            new ConceptoACalcular(1, 100m, 0, ObjetosDeImpuesto.SiObjeto, []))));

    [Fact]
    public void Descuento_mayor_que_el_importe_es_error()
        => Assert.Equal("descuento-mayor-que-importe", ClaveDeError(
            Comprobante(Concepto(1, 100m, 150m, Traslado(0.16m)))));

    [Fact]
    public void Mismo_impuesto_dos_veces_en_el_mismo_sentido_es_error()
        => Assert.Equal("impuesto-repetido", ClaveDeError(
            Comprobante(Concepto(1, 100m, 0, Traslado(0.16m), Traslado(0.08m)))));

    /// <summary>
    /// El mismo impuesto sí puede ir dos veces si uno es traslado y el otro retención: es
    /// justo el caso de honorarios.
    /// </summary>
    [Fact]
    public void Mismo_impuesto_como_traslado_y_como_retencion_es_valido()
    {
        var r = Calcular(Comprobante(Concepto(1, 100m, 0, Traslado(0.16m), Retencion(Iva, 0.106667m))));

        Assert.Equal(16m, r.TotalImpuestosTrasladados);
        Assert.Equal(10.666700m, r.TotalImpuestosRetenidos);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Cantidad_no_positiva_es_error(decimal cantidad)
        => Assert.Equal("cantidad-invalida", ClaveDeError(
            Comprobante(Concepto(cantidad, 100m, 0, Traslado(0.16m)))));

    [Fact]
    public void Comprobante_sin_conceptos_es_error()
        => Assert.Equal("sin-conceptos", ClaveDeError(new ComprobanteACalcular("MXN", null, [])));

    [Fact]
    public void Tasa_negativa_es_error()
        => Assert.Equal("tasa-negativa", ClaveDeError(
            Comprobante(Concepto(1, 100m, 0, Traslado(-0.16m)))));
}
