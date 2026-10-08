using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Documentos.Impuestos;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Documentos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Pagos;

/// <summary>
/// Complemento de pagos 2.0 (B9, §27 y §28 del documento funcional).
///
/// <para><b>El saldo se deriva, no se guarda</b></para>
/// No hay una columna «saldo» en la factura. El saldo pendiente es el total menos lo abonado
/// en los pagos emitidos o en proceso: un borrador no mueve el saldo y uno cancelado tampoco.
/// Mientras se confirma una emisión o cancelación, su importe sigue apartado.
/// Guardar un saldo obligaría a mantenerlo sincronizado desde tres sitios
/// distintos y a que los tres acertaran siempre.
///
/// <para><b>Lo que sí se congela</b></para>
/// Dentro del <c>DocumentoPagado</c>, el saldo anterior, lo pagado y el insoluto se guardan tal
/// como estaban al emitir: eso es lo que el SAT selló y no puede cambiar aunque después entren
/// más pagos (ARQUITECTURA.md §5).
/// </summary>
public sealed class ServicioDePagos(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno contexto,
    IServicioEmpresaEmisora empresaEmisora,
    IServicioClientes clientes,
    IServicioDeBitacora bitacora,
    HusoDeEmpresa huso,
    ILogger<ServicioDePagos> registro)
{
    /// <summary>Sin objeto de exportación: fuera del alcance del MVP (ARQUITECTURA.md §6).</summary>
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

        return ADto(comprobante, null);
    }

    public async Task<PagoDto?> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var comprobante = await CargarAsync(id, ct);
        return comprobante is null ? null : ADto(comprobante, comprobante.Pagos.FirstOrDefault());
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

        var (abonado, parcialidades) = await AbonadoAsync(uuid, ct);
        var saldo = factura.Total - abonado;

        if (saldo <= 0)
            return ErrorNegocio.Validacion(
                "factura-ya-saldada", "Esa factura ya está saldada: no le queda saldo pendiente.");

        return new DocumentoPorPagarDto(
            factura.Id,
            uuid,
            factura.Serie,
            factura.Folio?.ToString(),
            factura.Moneda,
            factura.Total,
            saldo,
            parcialidades + 1,
            ObjetoDeImpuestoDe(factura));
    }

    /// <summary>
    /// Los pagos en proceso apartan saldo; una cancelación pendiente todavía lo consume.
    /// Las parcialidades canceladas no se reutilizan, aunque su importe ya no consuma saldo.
    /// </summary>
    private async Task<(decimal Abonado, int Parcialidades)> AbonadoAsync(Guid uuid, CancellationToken ct)
    {
        var renglones = await baseDeDatos.PagosDocumentos
            .AsNoTracking()
            .Where(d => d.IdDocumento == uuid &&
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

        var documentos = await ResolverDocumentosAsync(peticion, receptor.Rfc, ct);
        if (documentos.EsFallo) return documentos.Error!;

        // Mismo criterio que en ServicioDeEmision: sin guardado previo, el registro es un alta.
        var antes = comprobante.ModificadoUtc is null ? null : ADto(comprobante, comprobante.Pagos.FirstOrDefault());

        AplicarCabecera(comprobante, receptor);
        AplicarPago(comprobante, peticion, documentos.Valor);

        comprobante.ModificadoUtc = DateTime.UtcNow;

        bitacora.Registrar(
            EntidadesDeBitacora.Comprobante, comprobante.Id.ToString(), AccionesDeBitacora.ComprobanteGuardado,
            antes, ADto(comprobante, comprobante.Pagos.FirstOrDefault()));

        await baseDeDatos.SaveChangesAsync(ct);

        registro.LogInformation(
            "Pago {Comprobante} guardado con {Documentos} documento(s).", comprobante.Id, documentos.Valor.Count);

        await transaccion.CommitAsync(ct);
        return ADto(comprobante, comprobante.Pagos.FirstOrDefault());
    }

    internal async Task<Resultado> ValidarSaldosParaEmisionAsync(Comprobante comprobante, CancellationToken ct)
    {
        if (comprobante.ClienteId is not { } clienteId || comprobante.Pagos.Count != 1)
            return ErrorNegocio.Validacion("pago-incompleto", "Guarda un pago completo antes de emitirlo.");
        var pago = comprobante.Pagos[0];
        var peticion = new PeticionGuardarPago(clienteId, pago.FechaPagoUtc, pago.FormaDePagoP,
            pago.MonedaP, pago.TipoCambioP, pago.Monto, pago.NumOperacion, pago.RfcEmisorCtaOrd,
            pago.NomBancoOrdExt, pago.CtaOrdenante, pago.RfcEmisorCtaBen, pago.CtaBeneficiario,
            [.. pago.Documentos.Select(d => new RenglonDePagoDto(d.IdDocumento, d.ImpPagado))]);
        var actuales = await ResolverDocumentosAsync(peticion, comprobante.ReceptorRfc, ct);
        if (actuales.EsFallo) return actuales.Error!;
        foreach (var actual in actuales.Valor)
        {
            var guardado = pago.Documentos.Single(d => d.IdDocumento == actual.Factura.Uuid);
            if (guardado.ImpSaldoAnt != actual.SaldoAnterior ||
                guardado.NumParcialidad != actual.NumParcialidad ||
                guardado.ImpSaldoInsoluto != actual.SaldoAnterior - guardado.ImpPagado)
                return ErrorNegocio.Conflicto("saldo-pago-desactualizado",
                    "Otro pago cambió el saldo o la parcialidad. Guarda de nuevo el borrador y revisa sus importes antes de emitir; no se reservó folio ni timbre.");
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
        if (decimales is null)
            return ErrorNegocio.Validacion("moneda-invalida", "Selecciona una moneda vigente del catálogo SAT; XXX no es una moneda de pago.");

        if (peticion.FormaDePagoP == "99" || !await baseDeDatos.SatFormasPago.AsNoTracking()
                .AnyAsync(x => x.Clave == peticion.FormaDePagoP && x.Vigente, ct))
            return ErrorNegocio.Validacion("forma-pago-invalida", "Selecciona la forma en que se recibió el pago; no puede ser por definir.");

        if (Math.Round(peticion.Monto, decimales.Value) != peticion.Monto ||
            peticion.Documentos.Any(d => Math.Round(d.ImpPagado, decimales.Value) != d.ImpPagado))
            return ErrorNegocio.Validacion("precision-pago-invalida", $"Los importes en {peticion.MonedaP} admiten {decimales.Value} decimales.");

        var suma = peticion.Documentos.Sum(d => d.ImpPagado);
        var monto = peticion.Monto;

        if (suma != monto)
            return ErrorNegocio.Validacion(
                "pago-no-cuadra",
                $"Lo repartido entre las facturas ({suma:N2}) no coincide con el importe del pago ({monto:N2}).");

        if (peticion.MonedaP != "MXN" && peticion.TipoCambioP is null)
            return ErrorNegocio.Validacion(
                "tipo-de-cambio-requerido",
                $"El pago está en {peticion.MonedaP}: hace falta el tipo de cambio.");
        else if (peticion.MonedaP != "MXN" && peticion.TipoCambioP is { } valor &&
                 (valor <= 0 || valor > maximoImporte || Math.Round(valor, 6) != valor))
            return ErrorNegocio.Validacion(
                "tipo-de-cambio-invalido", "El tipo de cambio debe ser positivo y tener hasta seis decimales.");

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
        PeticionGuardarPago peticion, string rfcReceptor, CancellationToken ct)
    {
        var resueltos = new List<DocumentoResuelto>(peticion.Documentos.Count);

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

            // La captura aún no admite EquivalenciaDR: nunca suponer 1 entre monedas distintas.
            if (factura.Moneda != peticion.MonedaP)
                return ErrorNegocio.Validacion("monedas-distintas-no-admitidas", $"La factura del renglón {i + 1} está en {factura.Moneda}. Por ahora el pago debe recibirse en esa misma moneda.");

            if (factura.MetodoPago != MetodoDiferido)
                return ErrorNegocio.Validacion(
                    "documento-no-es-ppd",
                    $"La factura del renglón {i + 1} no se emitió como PPD.");

            var (abonado, parcialidades) = await AbonadoAsync(linea.IdDocumento, ct);
            var saldoAnterior = factura.Total - abonado;

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

            resueltos.Add(new DocumentoResuelto(
                factura,
                saldoAnterior,
                linea.ImpPagado,
                parcialidades + 1,
                ImpuestosDe(factura)));
        }

        return resueltos;
    }

    private sealed record DocumentoResuelto(
        Comprobante Factura,
        decimal SaldoAnterior,
        decimal ImpPagado,
        int NumParcialidad,
        IReadOnlyList<ImpuestoDeFactura> Impuestos);

    /// <summary>
    /// Agrupa los impuestos de la factura como los necesita el reparto: por impuesto, factor,
    /// tasa y sentido, sumando la base y el importe de todos sus conceptos.
    /// </summary>
    private static IReadOnlyList<ImpuestoDeFactura> ImpuestosDe(Comprobante factura)
        => factura.Conceptos
            .SelectMany(c => c.Impuestos)
            .GroupBy(i => new { i.Impuesto, i.TipoFactor, i.TasaOCuota, i.EsRetencion })
            .Select(g => new ImpuestoDeFactura(
                g.Key.Impuesto,
                g.Key.TipoFactor,
                g.Key.TasaOCuota,
                g.Sum(x => x.Base),
                g.Any(x => x.Importe is not null) ? g.Sum(x => x.Importe ?? 0m) : null,
                g.Key.EsRetencion))
            .OrderBy(g => g.EsRetencion).ThenBy(g => g.Impuesto).ThenBy(g => g.TasaOCuota)
            .ToArray();

    /// <summary>
    /// <c>ObjetoImpDR</c> es 02 en cuanto la factura traiga un solo impuesto desglosado, y 01
    /// si no trae ninguno. Con 02 el desglose de <c>ImpuestosDR</c> pasa a ser obligatorio.
    /// </summary>
    private static string ObjetoDeImpuestoDe(Comprobante factura)
        => factura.Conceptos.Any(c => c.Impuestos.Count > 0)
            ? ObjetosDeImpuesto.SiObjeto
            : ObjetosDeImpuesto.NoObjeto;

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
            CtaBeneficiario = peticion.CtaBeneficiario
        };

        foreach (var resuelto in documentos)
        {
            var objetoImp = ObjetoDeImpuestoDe(resuelto.Factura);

            var documento = new DocumentoPagado
            {
                Id = Guid.NewGuid(),
                PagoId = pago.Id,
                ComprobantePagadoId = resuelto.Factura.Id,
                IdDocumento = resuelto.Factura.Uuid!.Value,
                Serie = resuelto.Factura.Serie,
                Folio = resuelto.Factura.Folio?.ToString(),
                MonedaDR = resuelto.Factura.Moneda,
                // Solo se soporta pagar en la misma moneda del documento: con monedas
                // distintas la equivalencia la fija el emisor y no hay pantalla que la capture.
                EquivalenciaDR = 1m,
                NumParcialidad = resuelto.NumParcialidad,
                ImpSaldoAnt = resuelto.SaldoAnterior,
                ImpPagado = resuelto.ImpPagado,
                ImpSaldoInsoluto = resuelto.SaldoAnterior - resuelto.ImpPagado,
                ObjetoImpDR = objetoImp
            };

            if (objetoImp == ObjetosDeImpuesto.SiObjeto)
            {
                var repartidos = CalculoDeImpuestosDePago.Repartir(
                    resuelto.Impuestos, resuelto.Factura.Total, resuelto.ImpPagado, 6);

                documento.Impuestos.AddRange(repartidos.Select(i => new ImpuestoDocumentoPagado
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
            }

            pago.Documentos.Add(documento);
        }

        comprobante.Pagos.Add(pago);
    }

    // ── Apoyo ───────────────────────────────────────────────────────────────────────────

    private Task<Comprobante?> CargarAsync(Guid id, CancellationToken ct)
        => baseDeDatos.Comprobantes
            .Include(c => c.Pagos).ThenInclude(p => p.Documentos).ThenInclude(d => d.Impuestos)
            .FirstOrDefaultAsync(
                c => c.Id == id && c.TipoDeComprobante == TiposDeComprobante.Pago, ct);

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
                        d.ObjetoImpDR))]);
}
