using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
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
/// en los pagos <b>ya timbrados</b>: un borrador de pago no debe mover el saldo de nada, y uno
/// cancelado tampoco. Guardar un saldo obligaría a mantenerlo sincronizado desde tres sitios
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
    ILogger<ServicioDePagos> registro)
{
    /// <summary>Sin objeto de exportación: fuera del alcance del MVP (ARQUITECTURA.md §6).</summary>
    private const string SinExportacion = "01";

    /// <summary>La raíz de un CFDI de pago va sin moneda; el dinero vive en el complemento.</summary>
    private const string MonedaSinValor = "XXX";

    /// <summary>Solo se puede pagar lo que se emitió con método «pago en parcialidades o diferido».</summary>
    private const string MetodoDiferido = "PPD";

    /// <summary>
    /// Formas de pago electrónicas: con ellas el SAT exige los datos bancarios (§28). El
    /// efectivo (01) y las demás no los llevan.
    /// </summary>
    private static readonly string[] FormasQueExigenBanco = ["02", "03", "04", "05", "28", "29"];

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
    /// Lo abonado a una factura y en cuántas parcialidades. Solo cuentan los pagos timbrados:
    /// un borrador no debe reducir el saldo de nada, y uno cancelado dejó de existir para el SAT.
    /// </summary>
    private async Task<(decimal Abonado, int Parcialidades)> AbonadoAsync(Guid uuid, CancellationToken ct)
    {
        var renglones = await baseDeDatos.PagosDocumentos
            .AsNoTracking()
            .Where(d => d.IdDocumento == uuid &&
                        d.Pago.Comprobante.Estatus == EstatusComprobante.Timbrado.ACadena())
            .Select(d => d.ImpPagado)
            .ToListAsync(ct);

        return (renglones.Sum(), renglones.Count);
    }

    // ── Guardado ────────────────────────────────────────────────────────────────────────

    public async Task<Resultado<PagoDto>> GuardarAsync(
        Guid id, PeticionGuardarPago peticion, CancellationToken ct)
    {
        var comprobante = await CargarAsync(id, ct);

        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("comprobante-no-encontrado", "Ese comprobante no existe.");

        if (comprobante.Estatus is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto(
                "comprobante-no-editable",
                $"El comprobante está en '{comprobante.Estatus}' y no se puede editar desde ahí.");

        if (peticion.Documentos.Count == 0)
            return ErrorNegocio.Validacion("pago-sin-documentos", "Agrega al menos una factura a pagar.");

        var validado = Validar(peticion);
        if (validado.EsFallo) return validado.Error!;

        var receptor = await clientes.ObtenerParaTimbradoAsync(peticion.ClienteId, ct);

        if (receptor is null)
            return ErrorNegocio.Validacion("cliente-no-encontrado", "Ese cliente no existe o está dado de baja.");

        var documentos = await ResolverDocumentosAsync(peticion, ct);
        if (documentos.EsFallo) return documentos.Error!;

        AplicarCabecera(comprobante, receptor);
        AplicarPago(comprobante, peticion, documentos.Valor);

        comprobante.ModificadoUtc = DateTime.UtcNow;

        await baseDeDatos.SaveChangesAsync(ct);

        registro.LogInformation(
            "Pago {Comprobante} guardado con {Documentos} documento(s).", comprobante.Id, documentos.Valor.Count);

        return ADto(comprobante, comprobante.Pagos.FirstOrDefault());
    }

    /// <summary>
    /// Las reglas de §27 y §28 que no dependen de la base: cuadre de la rejilla, tipo de
    /// cambio y datos bancarios.
    /// </summary>
    private static Resultado Validar(PeticionGuardarPago peticion)
    {
        if (peticion.Monto <= 0)
            return ErrorNegocio.Validacion("monto-invalido", "El importe del pago tiene que ser mayor que cero.");

        // §27: la suma de la rejilla tiene que cuadrar con el importe de la cabecera. Se
        // compara a dos decimales porque es la precisión en la que el usuario capturó.
        var suma = Math.Round(peticion.Documentos.Sum(d => d.ImpPagado), 2, MotorDeImpuestos.ModoDeRedondeo);
        var monto = Math.Round(peticion.Monto, 2, MotorDeImpuestos.ModoDeRedondeo);

        if (suma != monto)
            return ErrorNegocio.Validacion(
                "pago-no-cuadra",
                $"Lo repartido entre las facturas ({suma:N2}) no coincide con el importe del pago ({monto:N2}).");

        if (peticion.MonedaP != "MXN" && peticion.TipoCambioP is not { } tc)
            return ErrorNegocio.Validacion(
                "tipo-de-cambio-requerido",
                $"El pago está en {peticion.MonedaP}: hace falta el tipo de cambio.");
        else if (peticion.MonedaP != "MXN" && peticion.TipoCambioP is { } valor && valor <= 0)
            return ErrorNegocio.Validacion(
                "tipo-de-cambio-invalido", "El tipo de cambio tiene que ser mayor que cero.");

        // §28: con forma de pago electrónica, los datos del banco son obligatorios.
        if (FormasQueExigenBanco.Contains(peticion.FormaDePagoP))
        {
            if (string.IsNullOrWhiteSpace(peticion.CtaOrdenante))
                return ErrorNegocio.Validacion(
                    "cuenta-ordenante-requerida",
                    "Con esa forma de pago el SAT exige la cuenta ordenante.");

            if (string.IsNullOrWhiteSpace(peticion.CtaBeneficiario))
                return ErrorNegocio.Validacion(
                    "cuenta-beneficiaria-requerida",
                    "Con esa forma de pago el SAT exige la cuenta beneficiaria.");
        }

        return Resultado.Exito();
    }

    /// <summary>
    /// Vuelve a resolver cada factura contra la base y recalcula su saldo: lo que mandó el
    /// cliente es una propuesta, no un hecho. Entre que consultó y guardó pudo entrar otro
    /// pago, y aceptar su saldo a ciegas emitiría un complemento que ya no cuadra.
    /// </summary>
    private async Task<Resultado<IReadOnlyList<DocumentoResuelto>>> ResolverDocumentosAsync(
        PeticionGuardarPago peticion, CancellationToken ct)
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
                    resuelto.Impuestos, resuelto.Factura.Total, resuelto.ImpPagado, DecimalesDePresentacion);

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

    /// <summary>
    /// Los importes del complemento se calculan con los decimales de la moneda. Aquí se usa
    /// dos porque las monedas del MVP los tienen; el generador vuelve a formatear con los del
    /// catálogo al escribir el XML.
    /// </summary>
    private const int DecimalesDePresentacion = 2;

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
            pago?.FechaPagoUtc ?? comprobante.FechaEmisionUtc,
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
