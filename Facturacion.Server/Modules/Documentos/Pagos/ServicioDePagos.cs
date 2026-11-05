using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Documentos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Pagos;

/// <summary>Complemento de pagos 2.0. Calcula el saldo a partir de pagos emitidos o en proceso, no de borradores ni cancelados. Conserva los saldos y la parcialidad usados al aplicar el pago.</summary>
public sealed class ServicioDePagos(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno contexto,
    IServicioEmpresaEmisora empresaEmisora,
    IServicioClientes clientes,
    IServicioDeBitacora bitacora,
    HusoDeEmpresa huso,
    ILogger<ServicioDePagos> registro)
{
    /// <summary>Sin objeto de exportación: fuera del alcance del MVP.</summary>
    private const string SinExportacion = "01";

    /// <summary>La raíz de un CFDI de pago va sin moneda; el dinero vive en el complemento.</summary>
    private const string MonedaSinValor = "XXX";

    /// <summary>Solo se puede pagar lo que se emitió con método «pago en parcialidades o diferido».</summary>
    private const string MetodoDiferido = "PPD";

    // ── Alta ────────────────────────────────────────────────────────────────────────────

    public async Task<PagoDto> CrearBorradorAsync(CancellationToken ct)
    {
        var emisor = await empresaEmisora.ObtenerParaTimbradoAsync(ct);

        var comprobante = new Comprobante
        {
            Id = Guid.NewGuid(),
            Estatus = EstatusComprobante.Borrador.ACadena(),
            TipoDeComprobante = TiposDeComprobante.Pago,
            FechaEmisionUtc = DateTime.UtcNow,
            LugarExpedicion = emisor.CodigoPostalExpedicion,
            Moneda = MonedaSinValor,
            Exportacion = SinExportacion,
            EmisorRfc = emisor.Rfc,
            EmisorNombre = emisor.Nombre,
            EmisorRegimenFiscal = emisor.RegimenFiscal,
            ReceptorRfc = string.Empty,
            ReceptorNombre = string.Empty,
            ReceptorRegimenFiscal = string.Empty,
            ReceptorDomicilioFiscal = string.Empty,
            // El uso de un CFDI de pago siempre es CP01; el generador lo fija igualmente.
            ReceptorUsoCfdi = UsosDeCfdi.PagosDeCfdi,
            CreadoUtc = DateTime.UtcNow,
            CreadoPorUsuarioId = contexto.UsuarioActual ?? Guid.Empty
        };

        baseDeDatos.Comprobantes.Add(comprobante);
        await baseDeDatos.SaveChangesAsync(ct);

        return await ConDecimalesAsync(ADto(comprobante, null), ct);
    }

    public async Task<PagoDto?> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var comprobante = await CargarAsync(id, ct);
        return comprobante is null ? null : await ConDecimalesAsync(ADto(comprobante, comprobante.Pagos.FirstOrDefault()), ct);
    }

    public async Task<Resultado<PagoDto>> ObtenerParaSustituirAsync(Guid uuid, CancellationToken ct)
    {
        var origen = await SustitucionDePago.ResolverAsync(baseDeDatos, uuid, Guid.Empty, null, ct);
        if (origen.EsFallo) return origen.Error!;
        var comprobante = origen.Valor!;
        return await ConDecimalesAsync(ADto(comprobante, comprobante.Pagos[0]) with { UuidSustituido = uuid }, ct);
    }

    // ── Consulta de saldo (§27) ─────────────────────────────────────────────────────────

    /// <summary>
    /// El botón «Consulta» de §27: busca la factura por serie y folio y devuelve lo que se le
    /// debe y qué número de parcialidad sería este pago.
    /// </summary>
    public async Task<Resultado<DocumentoPorPagarDto>> ConsultarSaldoAsync(
        string serie, int folio, CancellationToken ct)
    {
        var factura = await baseDeDatos.Comprobantes
            .AsNoTracking()
            .Include(c => c.Conceptos).ThenInclude(x => x.Impuestos)
            .FirstOrDefaultAsync(
                c => c.Serie == serie && c.Folio == folio &&
                     c.TipoDeComprobante == TiposDeComprobante.Ingreso, ct);

        if (factura is null)
            return ErrorNegocio.NoEncontrado(
                "factura-no-encontrada", $"No hay una factura con serie {serie} y folio {folio}.");

        if (factura.Estatus != EstatusComprobante.Timbrado.ACadena() || factura.Uuid is not { } uuid)
            return ErrorNegocio.Validacion(
                "factura-no-timbrada",
                "Solo se puede registrar el pago de una factura timbrada.");

        // La regla de §27: el complemento de pagos solo aplica a lo emitido como PPD. Una
        // factura PUE ya se declaró pagada al emitirse, y volver a declararlo la duplica.
        if (factura.MetodoPago != MetodoDiferido)
            return ErrorNegocio.Validacion(
                "factura-no-es-ppd",
                $"Esa factura se emitió con método de pago «{factura.MetodoPago ?? "sin método"}». " +
                "El complemento de pagos solo aplica a las PPD.");

        if (await SustitucionDePago.TienePendienteAsync(baseDeDatos, uuid, ct))
            return ErrorNegocio.Conflicto("sustitucion-pago-pendiente", "Termina la cancelación del pago anterior antes de registrar otro pago.");
        var (abonado, parcialidades) = await AbonadoAsync(uuid, ct);
        var egresos = await Egresos.SaldoDeNotasCredito.ObtenerAsync(baseDeDatos, uuid, ct);
        if (egresos.EsFallo) return egresos.Error!;
        var saldo = factura.Total - abonado - egresos.Valor;

        if (saldo <= 0)
            return ErrorNegocio.Validacion(
                "factura-ya-saldada", "Esa factura ya está saldada: no le queda saldo pendiente.");

        var desglose = await Egresos.SaldoDeNotasCredito.CalcularDesgloseAsync(baseDeDatos, factura, saldo, ct);
        if (desglose.EsFallo) return desglose.Error!;

        return new DocumentoPorPagarDto(
            factura.Id,
            uuid,
            factura.Serie,
            factura.Folio?.ToString(),
            factura.Moneda,
            factura.Total,
            saldo,
            parcialidades + 1,
            desglose.Valor.ObjetoImpDR,
            await baseDeDatos.SatMonedas.Where(x => x.Clave == factura.Moneda).Select(x => x.Decimales).SingleAsync(ct));
    }

    /// <summary>
    /// Los pagos en proceso apartan saldo; una cancelación pendiente todavía lo consume.
    /// Las parcialidades canceladas no se reutilizan, aunque su importe ya no consuma saldo.
    /// </summary>
    private async Task<(decimal Abonado, int Parcialidades)> AbonadoAsync(Guid uuid, CancellationToken ct, Guid? excluir = null)
    {
        var renglones = await baseDeDatos.PagosDocumentos
            .AsNoTracking()
            .Where(d => d.IdDocumento == uuid && d.Pago.ComprobanteId != excluir &&
                (d.Pago.Comprobante.Estatus == "timbrando" || d.Pago.Comprobante.Estatus == "timbrado" ||
                 d.Pago.Comprobante.Estatus == "en_cancelacion" || d.Pago.Comprobante.Estatus == "cancelado"))
            .Select(d => new { d.ImpPagado, d.NumParcialidad, d.Pago.Comprobante.Estatus })
            .ToListAsync(ct);

        return (renglones.Where(d => d.Estatus != "cancelado").Sum(d => d.ImpPagado),
            renglones.Count == 0 ? 0 : renglones.Max(d => d.NumParcialidad));
    }

    // ── Guardado ────────────────────────────────────────────────────────────────────────

    public async Task<Resultado<PagoDto>> GuardarAsync(
        Guid id, PeticionGuardarPago peticion, CancellationToken ct)
    {
        peticion = peticion with
        {
            TipoCadPago = Opcional(peticion.TipoCadPago), CertPago = Opcional(peticion.CertPago),
            CadPago = Opcional(peticion.CadPago), SelloPago = Opcional(peticion.SelloPago)
        };
        if (peticion.FechaPagoLocal is { } local)
        {
            var zona = await huso.ObtenerAsync(ct);
            if (local == default || local.Kind != DateTimeKind.Unspecified ||
                zona.IsInvalidTime(local) || zona.IsAmbiguousTime(local))
                return ErrorNegocio.Validacion("fecha-pago-invalida",
                    "La fecha y hora del pago no son válidas o son ambiguas en el huso de la empresa.");
            try
            {
                peticion = peticion with { FechaPagoUtc = TimeZoneInfo.ConvertTimeToUtc(local, zona) };
            }
            catch (ArgumentException)
            {
                return ErrorNegocio.Validacion("fecha-pago-invalida", "La fecha del pago está fuera del rango permitido.");
            }
        }

        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(ct);
        await BloqueoDePagos.TomarAsync(baseDeDatos, contexto.EmpresaActual
            ?? throw new InvalidOperationException("Falta empresa activa."), ct);
        var comprobante = await CargarAsync(id, ct);

        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("comprobante-no-encontrado", "Ese comprobante no existe.");

        if (PermisoDeEmision.ExigirEdicion(contexto, comprobante) is { } sinPermiso)
            return sinPermiso;

        if (comprobante.Estatus is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto(
                "comprobante-no-editable",
                $"El comprobante está en '{comprobante.Estatus}' y no se puede editar desde ahí.");

        if (peticion.Documentos is null || peticion.Documentos.Count == 0)
            return ErrorNegocio.Validacion("pago-sin-documentos", "Agrega al menos una factura a pagar.");

        var validado = await ValidarAsync(peticion, ct);
        if (validado.EsFallo) return validado.Error!;

        var receptor = await clientes.ObtenerParaTimbradoAsync(peticion.ClienteId, ct);

        if (receptor is null)
            return ErrorNegocio.Validacion("cliente-no-encontrado", "Ese cliente no existe o está dado de baja.");

        var documentos = await ResolverDocumentosAsync(peticion, receptor.Rfc, id, ct);
        if (documentos.EsFallo) return documentos.Error!;
        var precisionPago = await baseDeDatos.SatMonedas.Where(x => x.Clave == peticion.MonedaP).Select(x => x.Decimales).SingleAsync(ct);
        if (!ConversionDePago.Cuadra(peticion.Monto, precisionPago, documentos.Valor.Select(x =>
            (x.ImpPagado, x.EquivalenciaDR, x.DecimalesMoneda, x.Factura.Moneda == peticion.MonedaP))))
            return ErrorNegocio.Validacion("pago-no-cuadra", "El monto recibido no coincide con los abonos convertidos a la moneda del pago.");

        // Mismo criterio que en ServicioDeEmision: sin guardado previo, el registro es un alta.
        var antes = comprobante.ModificadoUtc is null ? null : ADto(comprobante, comprobante.Pagos.FirstOrDefault());

        AplicarCabecera(comprobante, receptor);
        AplicarPago(comprobante, peticion, documentos.Valor);
        baseDeDatos.ComprobantesRelacionados.RemoveRange(comprobante.Relacionados);
        comprobante.Relacionados.Clear();
        if (peticion.UuidSustituido is { } uuidSustituido)
            comprobante.Relacionados.Add(new ComprobanteRelacionado
            {
                Id = Guid.NewGuid(), ComprobanteId = comprobante.Id,
                TipoRelacion = "04", UuidRelacionado = uuidSustituido
            });

        if (GeneradorDeXmlPago.Validar(comprobante, precisionPago) is { } errorPago)
            return errorPago;

        comprobante.ModificadoUtc = DateTime.UtcNow;

        bitacora.Registrar(
            EntidadesDeBitacora.Comprobante, comprobante.Id.ToString(), AccionesDeBitacora.ComprobanteGuardado,
            antes, ADto(comprobante, comprobante.Pagos.FirstOrDefault()));

        await baseDeDatos.SaveChangesAsync(ct);

        registro.LogInformation(
            "Pago {Comprobante} guardado con {Documentos} documento(s).", comprobante.Id, documentos.Valor.Count);

        await transaccion.CommitAsync(ct);
        return await ConDecimalesAsync(ADto(comprobante, comprobante.Pagos.FirstOrDefault()), ct);
    }

    internal async Task<Resultado> ValidarSaldosParaEmisionAsync(Comprobante comprobante, CancellationToken ct)
    {
        if (comprobante.ClienteId is not { } clienteId || comprobante.Pagos.Count != 1)
            return ErrorNegocio.Validacion("pago-incompleto", "Guarda un pago completo antes de emitirlo.");
        var pago = comprobante.Pagos[0];
        var peticion = new PeticionGuardarPago(clienteId, pago.FechaPagoUtc, pago.FormaDePagoP,
            pago.MonedaP, pago.TipoCambioP, pago.Monto, pago.NumOperacion, pago.RfcEmisorCtaOrd,
            pago.NomBancoOrdExt, pago.CtaOrdenante, pago.RfcEmisorCtaBen, pago.CtaBeneficiario,
            [.. pago.Documentos.Select(d => new RenglonDePagoDto(d.IdDocumento, d.ImpPagado, d.EquivalenciaDR))])
        {
            UuidSustituido = comprobante.Relacionados.FirstOrDefault(r => r.TipoRelacion == "04")?.UuidRelacionado,
            TipoCadPago = pago.TipoCadPago, CertPago = pago.CertPago, CadPago = pago.CadPago, SelloPago = pago.SelloPago
        };
        var actuales = await ResolverDocumentosAsync(peticion, comprobante.ReceptorRfc, comprobante.Id, ct);
        if (actuales.EsFallo) return actuales.Error!;
        foreach (var actual in actuales.Valor)
        {
            var guardado = pago.Documentos.Single(d => d.IdDocumento == actual.Factura.Uuid);
            if (guardado.ImpSaldoAnt != actual.SaldoAnterior ||
                guardado.DecimalesMonedaDR != actual.DecimalesMoneda ||
                guardado.NumParcialidad != actual.NumParcialidad ||
                guardado.ImpSaldoInsoluto != actual.SaldoAnterior - guardado.ImpPagado)
                return ErrorNegocio.Conflicto("saldo-pago-desactualizado",
                    "Un pago o nota de crédito cambió el saldo o la parcialidad. Guarda de nuevo el borrador y revisa sus importes antes de emitir; no se reservó folio ni timbre.");
            if (guardado.MonedaDR != actual.Factura.Moneda || !actual.Desglose.CoincideCon(guardado))
                return ErrorNegocio.Conflicto("desglose-pago-desactualizado",
                    "Los impuestos guardados no coinciden con la factura. Guarda nuevamente el borrador antes de emitirlo; no se reservó folio ni timbre.");
        }
        return Resultado.Exito();
    }


    public async Task<Resultado> EliminarBorradorAsync(Guid id, CancellationToken ct)
    {
        await using var transaccion = await baseDeDatos.Database.BeginTransactionAsync(ct);
        await BloqueoDePagos.TomarAsync(baseDeDatos, contexto.EmpresaActual
            ?? throw new InvalidOperationException("Falta empresa activa."), ct);
        var comprobante = await CargarAsync(id, ct);

        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("comprobante-no-encontrado", "Ese comprobante no existe.");

        // Mismo criterio que en ServicioDeEmision: solo se descarta lo que nunca se intentó timbrar.
        if (comprobante.Estatus != EstatusComprobante.Borrador.ACadena())
            return ErrorNegocio.Conflicto(
                "comprobante-no-es-borrador",
                "Solo se puede descartar un comprobante que nunca se intentó timbrar.");

        // Y el mismo criterio de ServicioDeEmision para la bitácora: un borrador que nunca se
        // guardó no llevaba datos fiscales que dejar registrados.
        if (comprobante.ModificadoUtc is not null)
            bitacora.Registrar(
                EntidadesDeBitacora.Comprobante, comprobante.Id.ToString(), AccionesDeBitacora.BorradorDescartado,
                antes: ADto(comprobante, comprobante.Pagos.FirstOrDefault()));

        baseDeDatos.Comprobantes.Remove(comprobante);
        await baseDeDatos.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        return Resultado.Exito();
    }


    /// <summary>
    /// Valida la captura y las claves vigentes del SAT antes de resolver las facturas.
    /// </summary>
    private async Task<Resultado> ValidarAsync(PeticionGuardarPago peticion, CancellationToken ct)
    {
        if (ReglasDeCadenaPago.Validar(peticion.FormaDePagoP, peticion.TipoCadPago,
            peticion.CertPago, peticion.CadPago, peticion.SelloPago) is { } cadenaInvalida)
            return cadenaInvalida;
        const decimal maximoImporte = 999999999999.999999m;
        if (peticion.Monto <= 0 || peticion.Monto > maximoImporte ||
            peticion.Documentos.Any(d => d.ImpPagado <= 0 || d.ImpPagado > maximoImporte))
            return ErrorNegocio.Validacion("monto-invalido", "Captura importes positivos de hasta doce dígitos enteros.");

        if (peticion.Documentos.Count > 100)
            return ErrorNegocio.Validacion("demasiados-documentos", "Un pago admite hasta 100 facturas.");

        if (peticion.Documentos.Select(d => d.IdDocumento).Distinct().Count() != peticion.Documentos.Count)
            return ErrorNegocio.Validacion("documento-repetido", "Una factura no puede aparecer dos veces en el mismo pago.");

        if (peticion.FechaPagoUtc == default || peticion.FechaPagoUtc.Kind != DateTimeKind.Utc)
            return ErrorNegocio.Validacion("fecha-pago-invalida", "Captura una fecha válida para el pago.");

        var decimales = await baseDeDatos.SatMonedas.AsNoTracking()
            .Where(x => x.Clave == peticion.MonedaP && x.Vigente && x.Clave != "XXX")
            .Select(x => (int?)x.Decimales).FirstOrDefaultAsync(ct);
        if (decimales is null or < 0 or > 6)
            return ErrorNegocio.Validacion("moneda-invalida", "Selecciona una moneda vigente del catálogo SAT; XXX no es una moneda de pago.");

        if (await MonedaHabilitada.ExigirAsync(baseDeDatos, peticion.MonedaP, ct) is { } monedaNoHabilitada)
            return monedaNoHabilitada;

        if (peticion.FormaDePagoP == "99" || !await baseDeDatos.SatFormasPago.AsNoTracking()
                .AnyAsync(x => x.Clave == peticion.FormaDePagoP && x.Vigente, ct))
            return ErrorNegocio.Validacion("forma-pago-invalida", "Selecciona la forma en que se recibió el pago; no puede ser por definir.");

        if (Math.Round(peticion.Monto, decimales.Value) != peticion.Monto)
            return ErrorNegocio.Validacion("precision-pago-invalida", $"Los importes en {peticion.MonedaP} admiten {decimales.Value} decimales.");

        if (peticion.MonedaP != "MXN" && peticion.TipoCambioP is null)
            return ErrorNegocio.Validacion(
                "tipo-de-cambio-requerido",
                $"El pago está en {peticion.MonedaP}: hace falta el tipo de cambio.");
        else if (peticion.MonedaP != "MXN" && peticion.TipoCambioP is { } valor &&
                 (valor <= 0 || valor > maximoImporte || Math.Round(valor, 6) != valor))
            return ErrorNegocio.Validacion(
                "tipo-de-cambio-invalido", "El tipo de cambio debe ser positivo y tener hasta seis decimales.");

        if (peticion.MonedaP == "MXN" && peticion.TipoCambioP is not (null or 1m))
            return ErrorNegocio.Validacion("tipo-de-cambio-invalido", "El tipo de cambio en pesos debe ser 1.");

        if (peticion.NumOperacion?.Length > 100 || peticion.NomBancoOrdExt?.Length > 300 ||
            peticion.CtaOrdenante?.Length > 50 || peticion.CtaBeneficiario?.Length > 50 ||
            peticion.RfcEmisorCtaOrd?.Length > 13 || peticion.RfcEmisorCtaBen?.Length > 13)
            return ErrorNegocio.Validacion("datos-bancarios-demasiado-largos", "Revisa la longitud de la referencia y de los datos bancarios.");

        return Resultado.Exito();
    }

    /// <summary>
    /// Vuelve a resolver cada factura contra la base y recalcula su saldo: lo que mandó el
    /// cliente es una propuesta, no un hecho. Entre que consultó y guardó pudo entrar otro
    /// pago, y aceptar su saldo a ciegas emitiría un complemento que ya no cuadra.
    /// </summary>
    private async Task<Resultado<IReadOnlyList<DocumentoResuelto>>> ResolverDocumentosAsync(
        PeticionGuardarPago peticion, string rfcReceptor, Guid comprobanteId, CancellationToken ct)
    {
        var resueltos = new List<DocumentoResuelto>(peticion.Documentos.Count);
        var origen = await SustitucionDePago.ResolverAsync(baseDeDatos, peticion.UuidSustituido,
            comprobanteId, peticion.ClienteId, ct);
        if (origen.EsFallo) return origen.Error!;
        var sustituido = origen.Valor;
        if (sustituido is not null && (sustituido.ReceptorRfc != rfcReceptor ||
            !sustituido.Pagos[0].Documentos.Select(d => d.IdDocumento).ToHashSet()
                .SetEquals(peticion.Documentos.Select(d => d.IdDocumento))))
            return ErrorNegocio.Validacion("sustitucion-documentos-distintos", "Conserva el receptor y las mismas facturas del pago anterior.");

        for (var i = 0; i < peticion.Documentos.Count; i++)
        {
            var linea = peticion.Documentos[i];

            var factura = await baseDeDatos.Comprobantes
                .AsNoTracking()
                .Include(c => c.Conceptos).ThenInclude(x => x.Impuestos)
                .FirstOrDefaultAsync(c => c.Uuid == linea.IdDocumento, ct);

            if (factura is null)
                return ErrorNegocio.Validacion(
                    "documento-no-encontrado",
                    $"La factura del renglón {i + 1} no existe en esta empresa.");

            if (factura.TipoDeComprobante != TiposDeComprobante.Ingreso || factura.Estatus != "timbrado")
                return ErrorNegocio.Validacion("documento-no-pagable", $"El renglón {i + 1} debe ser una factura de ingreso timbrada y vigente.");

            if (factura.ClienteId != peticion.ClienteId || factura.ReceptorRfc != rfcReceptor)
                return ErrorNegocio.Validacion("documento-de-otro-cliente", $"La factura del renglón {i + 1} no corresponde al cliente seleccionado.");

            if (!ConversionDePago.EsEquivalenciaValida(factura.Moneda, peticion.MonedaP, linea.EquivalenciaDR))
                return ErrorNegocio.Validacion("equivalencia-pago-invalida", $"Revisa la equivalencia del renglón {i + 1}: en la misma moneda es 1; entre monedas distintas se requiere un valor positivo con hasta diez decimales.");
            var precision = await baseDeDatos.SatMonedas.Where(x => x.Clave == factura.Moneda).Select(x => (int?)x.Decimales).SingleOrDefaultAsync(ct);
            if (precision is null || precision is < 0 or > 6 || Math.Round(linea.ImpPagado, precision.Value) != linea.ImpPagado)
                return ErrorNegocio.Validacion("precision-documento-pagado-invalida", $"El abono del renglón {i + 1} debe respetar los decimales de la moneda de la factura.");

            if (factura.MetodoPago != MetodoDiferido)
                return ErrorNegocio.Validacion(
                    "documento-no-es-ppd",
                    $"La factura del renglón {i + 1} no se emitió como PPD.");

            if (sustituido is null && await SustitucionDePago.TienePendienteAsync(baseDeDatos, linea.IdDocumento, ct))
                return ErrorNegocio.Conflicto("sustitucion-pago-pendiente", "Termina la cancelación del pago anterior antes de registrar otro pago.");
            var (abonado, parcialidades) = await AbonadoAsync(linea.IdDocumento, ct, sustituido?.Id);
            var egresos = await Egresos.SaldoDeNotasCredito.ObtenerAsync(baseDeDatos, linea.IdDocumento, ct);
            if (egresos.EsFallo) return egresos.Error!;
            var saldoAnterior = factura.Total - abonado - egresos.Valor;

            if (linea.ImpPagado <= 0)
                return ErrorNegocio.Validacion(
                    "importe-invalido",
                    $"El importe del renglón {i + 1} tiene que ser mayor que cero.");

            // §27: el saldo actual no puede quedar negativo.
            if (linea.ImpPagado > saldoAnterior)
                return ErrorNegocio.Validacion(
                    "importe-mayor-que-saldo",
                    $"El renglón {i + 1} abona {linea.ImpPagado:N2} a una factura que solo debe " +
                    $"{saldoAnterior:N2}.");

            var desglose = await Egresos.SaldoDeNotasCredito.CalcularDesgloseAsync(baseDeDatos, factura,
                linea.ImpPagado, ct, sustituido?.Id, comprobanteId);
            if (desglose.EsFallo) return desglose.Error!;
            resueltos.Add(new DocumentoResuelto(
                factura,
                saldoAnterior,
                linea.ImpPagado,
                sustituido?.Pagos[0].Documentos.Single(d => d.IdDocumento == linea.IdDocumento).NumParcialidad ?? parcialidades + 1,
                desglose.Valor, linea.EquivalenciaDR ?? 1m, precision.Value));
        }

        return resueltos;
    }

    private sealed record DocumentoResuelto(
        Comprobante Factura,
        decimal SaldoAnterior,
        decimal ImpPagado,
        int NumParcialidad,
        DesgloseDeFacturaParaPago Desglose, decimal EquivalenciaDR, int DecimalesMoneda);

    // ── Aplicar ─────────────────────────────────────────────────────────────────────────

    private static void AplicarCabecera(Comprobante comprobante, ReceptorFiscalDto receptor)
    {
        comprobante.ClienteId = receptor.ClienteId;
        comprobante.ReceptorRfc = receptor.Rfc;
        comprobante.ReceptorNombre = receptor.Nombre;
        comprobante.ReceptorRegimenFiscal = receptor.RegimenFiscal;
        comprobante.ReceptorDomicilioFiscal = receptor.DomicilioFiscalCp;
        comprobante.ReceptorUsoCfdi = UsosDeCfdi.PagosDeCfdi;

        // La raíz de un CFDI de pago declara cero en todo: el dinero está en el complemento.
        comprobante.Moneda = MonedaSinValor;
        comprobante.TipoCambio = null;
        comprobante.FormaPago = null;
        comprobante.MetodoPago = null;
        comprobante.SubTotal = 0m;
        comprobante.Descuento = 0m;
        comprobante.TotalImpuestosTrasladados = 0m;
        comprobante.TotalImpuestosRetenidos = 0m;
        comprobante.Total = 0m;
    }

    private void AplicarPago(
        Comprobante comprobante, PeticionGuardarPago peticion, IReadOnlyList<DocumentoResuelto> documentos)
    {
        // Se reemplaza entero en vez de reconciliar renglón por renglón: son pocos y el
        // borrador se guarda completo cada vez, igual que en la emisión.
        baseDeDatos.Pagos.RemoveRange(comprobante.Pagos);
        comprobante.Pagos.Clear();

        var pago = new Pago
        {
            Id = Guid.NewGuid(),
            ComprobanteId = comprobante.Id,
            FechaPagoUtc = peticion.FechaPagoUtc,
            FormaDePagoP = peticion.FormaDePagoP,
            MonedaP = peticion.MonedaP,
            TipoCambioP = peticion.MonedaP == "MXN" ? null : peticion.TipoCambioP,
            Monto = peticion.Monto,
            NumOperacion = peticion.NumOperacion,
            RfcEmisorCtaOrd = peticion.RfcEmisorCtaOrd,
            NomBancoOrdExt = peticion.NomBancoOrdExt,
            CtaOrdenante = peticion.CtaOrdenante,
            RfcEmisorCtaBen = peticion.RfcEmisorCtaBen,
            CtaBeneficiario = peticion.CtaBeneficiario,
            TipoCadPago = peticion.TipoCadPago, CertPago = peticion.CertPago,
            CadPago = peticion.CadPago, SelloPago = peticion.SelloPago
        };

        foreach (var resuelto in documentos)
        {
            var documento = new DocumentoPagado
            {
                Id = Guid.NewGuid(),
                PagoId = pago.Id,
                ComprobantePagadoId = resuelto.Factura.Id,
                IdDocumento = resuelto.Factura.Uuid!.Value,
                Serie = resuelto.Factura.Serie,
                Folio = resuelto.Factura.Folio?.ToString(),
                MonedaDR = resuelto.Factura.Moneda,
                EquivalenciaDR = resuelto.EquivalenciaDR,
                DecimalesMonedaDR = resuelto.DecimalesMoneda,
                NumParcialidad = resuelto.NumParcialidad,
                ImpSaldoAnt = resuelto.SaldoAnterior,
                ImpPagado = resuelto.ImpPagado,
                ImpSaldoInsoluto = resuelto.SaldoAnterior - resuelto.ImpPagado,
                ObjetoImpDR = resuelto.Desglose.ObjetoImpDR
            };

            documento.Impuestos.AddRange(resuelto.Desglose.Impuestos.Select(i => new ImpuestoDocumentoPagado
            {
                Id = Guid.NewGuid(),
                DocumentoPagadoId = documento.Id,
                Impuesto = i.Impuesto,
                TipoFactor = i.TipoFactor,
                TasaOCuota = i.TasaOCuota,
                Base = i.Base,
                Importe = i.Importe,
                EsRetencion = i.EsRetencion
            }));
            pago.Documentos.Add(documento);
        }

        comprobante.Pagos.Add(pago);
    }

    // ── Apoyo ───────────────────────────────────────────────────────────────────────────

    private Task<Comprobante?> CargarAsync(Guid id, CancellationToken ct)
        => baseDeDatos.Comprobantes
            .Include(c => c.Relacionados)
            .Include(c => c.Pagos).ThenInclude(p => p.Documentos).ThenInclude(d => d.Impuestos)
            .FirstOrDefaultAsync(
                c => c.Id == id && c.TipoDeComprobante == TiposDeComprobante.Pago, ct);

    private async Task<PagoDto> ConDecimalesAsync(PagoDto pago, CancellationToken ct)
        => pago with { DecimalesPorMoneda = await baseDeDatos.SatMonedas.AsNoTracking().ToDictionaryAsync(x => x.Clave, x => x.Decimales, ct) };

    private static string? Opcional(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto;

    private static PagoDto ADto(Comprobante comprobante, Pago? pago)
        => new(
            comprobante.Id,
            comprobante.Estatus,
            comprobante.Serie,
            comprobante.Folio,
            comprobante.ClienteId,
            comprobante.ReceptorRfc,
            comprobante.ReceptorNombre,
            DateTime.SpecifyKind(pago?.FechaPagoUtc ?? comprobante.FechaEmisionUtc, DateTimeKind.Utc),
            pago?.FormaDePagoP,
            pago?.MonedaP ?? "MXN",
            pago?.TipoCambioP,
            pago?.Monto ?? 0m,
            pago?.NumOperacion,
            pago?.RfcEmisorCtaOrd,
            pago?.NomBancoOrdExt,
            pago?.CtaOrdenante,
            pago?.RfcEmisorCtaBen,
            pago?.CtaBeneficiario,
            comprobante.Uuid,
            pago is null
                ? []
                : [.. pago.Documentos
                    .OrderBy(d => d.NumParcialidad)
                    .Select(d => new DocumentoPagadoDto(
                        d.IdDocumento,
                        d.Serie,
                        d.Folio,
                        d.MonedaDR,
                        d.NumParcialidad,
                        d.ImpSaldoAnt,
                        d.ImpPagado,
                        d.ImpSaldoInsoluto,
                        d.ObjetoImpDR, d.EquivalenciaDR, d.DecimalesMonedaDR))])
        {
            UuidSustituido = comprobante.Relacionados.FirstOrDefault(r => r.TipoRelacion == "04")?.UuidRelacionado,
            TipoCadPago = pago?.TipoCadPago, CertPago = pago?.CertPago, CadPago = pago?.CadPago, SelloPago = pago?.SelloPago
        };
}
