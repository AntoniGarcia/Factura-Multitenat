using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Documentos.Impuestos;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Documentos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Emision;

/// <summary>
/// Alta y edición de borradores de factura estándar (§12 del documento funcional).
///
/// <para><b>El comprobante es el borrador y el timbrado: la misma fila</b></para>
/// No hay una tabla de borradores aparte. Se edita el mismo <see cref="Comprobante"/> mientras
/// está en <c>borrador</c> o <c>error</c>; al confirmarlo con éxito, <c>ServicioDeTimbrado</c>
/// lo congela. Los campos del receptor ya son copias desde B0 —nunca llaves foráneas—, así que
/// "congelar" no cambia la forma del dato: solo deja de reescribirse.
///
/// <para><b>Por qué los totales se calculan y se guardan aquí, no al timbrar</b></para>
/// <see cref="Salidas.GeneradorDeXmlCfdi"/> lee <c>Concepto.Importe</c> e
/// <c>ImpuestoConcepto.Base/Importe</c> directo de la base: no vuelve a invocar el motor. Si
/// esos valores no quedaran correctos desde que se guarda el borrador, el XML saldría con
/// números que nadie calculó a propósito.
/// </summary>
public sealed class ServicioDeEmision(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno contexto,
    IServicioEmpresaEmisora empresaEmisora,
    IServicioClientes clientes,
    IServicioProductos productos)
{
    /// <summary>Único tipo de comprobante de esta fase: factura de ingreso estándar.</summary>
    private const string TipoFactura = "I";

    /// <summary>Sin objeto de exportación: fuera del alcance del MVP (CLAUDE.md §6).</summary>
    private const string SinExportacion = "01";

    public async Task<ComprobanteDto> CrearBorradorAsync(CancellationToken ct)
    {
        var emisor = await empresaEmisora.ObtenerParaTimbradoAsync(ct);

        var comprobante = new Comprobante
        {
            Id = Guid.NewGuid(),
            Estatus = EstatusComprobante.Borrador.ACadena(),
            TipoDeComprobante = TipoFactura,
            FechaEmisionUtc = DateTime.UtcNow,
            LugarExpedicion = emisor.CodigoPostalExpedicion,
            Moneda = "MXN",
            Exportacion = SinExportacion,
            EmisorRfc = emisor.Rfc,
            EmisorNombre = emisor.Nombre,
            EmisorRegimenFiscal = emisor.RegimenFiscal,
            // Vacíos hasta que se elija cliente. 'required' de C# solo exige un valor al
            // construir, no que sea uno con sentido: aquí lo tiene, porque todavía no lo hay.
            ReceptorRfc = string.Empty,
            ReceptorNombre = string.Empty,
            ReceptorRegimenFiscal = string.Empty,
            ReceptorDomicilioFiscal = string.Empty,
            ReceptorUsoCfdi = string.Empty,
            CreadoUtc = DateTime.UtcNow,
            CreadoPorUsuarioId = contexto.UsuarioActual ?? Guid.Empty
        };

        baseDeDatos.Comprobantes.Add(comprobante);
        await baseDeDatos.SaveChangesAsync(ct);

        return ADto(comprobante);
    }

    public async Task<ComprobanteDto?> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var comprobante = await CargarAsync(id, ct);
        return comprobante is null ? null : ADto(comprobante);
    }

    public async Task<Resultado<ComprobanteDto>> GuardarAsync(
        Guid id, PeticionGuardarBorrador peticion, CancellationToken ct)
    {
        var comprobante = await CargarAsync(id, ct);

        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("comprobante-no-encontrado", "Ese comprobante no existe.");

        // Igual que ApartarAsync en ServicioDeTimbrado: se edita desde borrador o desde un
        // error ya resuelto —ahí el folio sigue apartado y se reutiliza al reintentar—, nunca
        // mientras hay un intento en vuelo o después de quedar timbrado.
        if (comprobante.Estatus is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto(
                "comprobante-no-editable",
                $"El comprobante está en '{comprobante.Estatus}' y no se puede editar desde ahí.");

        if (peticion.Conceptos.Count == 0)
            return ErrorNegocio.Validacion("sin-conceptos", "Agrega al menos un concepto.");

        var receptor = await ResolverReceptorAsync(peticion.ClienteId, ct);
        if (receptor.EsFallo) return receptor.Error!;

        var conceptosResueltos = await ResolverConceptosAsync(peticion.Conceptos, ct);
        if (conceptosResueltos.EsFallo) return conceptosResueltos.Error!;

        var calculado = MotorDeImpuestos.Calcular(new ComprobanteACalcular(
            peticion.Moneda,
            peticion.TipoCambio,
            [.. conceptosResueltos.Valor.Select(c => c.ACalcular)]));

        if (calculado.EsFallo) return calculado.Error!;

        AplicarCabecera(comprobante, peticion, receptor.Valor);
        AplicarConceptos(comprobante, conceptosResueltos.Valor, calculado.Valor);
        AplicarRelacionados(comprobante, peticion.Relacionados);
        AplicarTotales(comprobante, calculado.Valor);

        comprobante.ModificadoUtc = DateTime.UtcNow;

        await baseDeDatos.SaveChangesAsync(ct);

        return ADto(comprobante);
    }

    /// <summary>
    /// Solo para un borrador nunca timbrado (CLAUDE.md §5, la única excepción a «nada se
    /// borra»). Uno en <c>error</c> ya tomó folio: descartarlo dejaría un hueco sin explicación
    /// en la numeración, así que se descarta re-timbrando o dejándolo ahí, nunca borrándolo.
    /// </summary>
    public async Task<Resultado> EliminarBorradorAsync(Guid id, CancellationToken ct)
    {
        var comprobante = await baseDeDatos.Comprobantes.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (comprobante is null)
            return ErrorNegocio.NoEncontrado("comprobante-no-encontrado", "Ese comprobante no existe.");

        if (comprobante.Estatus != "borrador")
            return ErrorNegocio.Conflicto(
                "comprobante-no-es-borrador",
                "Solo se puede descartar un comprobante que nunca se intentó timbrar.");

        baseDeDatos.Comprobantes.Remove(comprobante);
        await baseDeDatos.SaveChangesAsync(ct);

        return Resultado.Exito();
    }

    /// <summary>Resuelve un CFDI relacionado por Serie+Folio, entre lo timbrado de esta empresa.</summary>
    public async Task<Resultado<CfdiRelacionadoResueltoDto>> ResolverRelacionadoAsync(
        string serie, int folio, CancellationToken ct)
    {
        var relacionado = await baseDeDatos.Comprobantes
            .AsNoTracking()
            .Where(c => c.Serie == serie && c.Folio == folio && c.Uuid != null)
            .Select(c => new CfdiRelacionadoResueltoDto(c.Uuid!.Value, c.Serie!, c.Folio!.Value))
            .FirstOrDefaultAsync(ct);

        return relacionado is null
            ? ErrorNegocio.NoEncontrado(
                "cfdi-relacionado-no-encontrado",
                $"No se encontró un CFDI timbrado con serie {serie} y folio {folio}.")
            : relacionado;
    }

    /// <summary>
    /// El listado de documentos de §1 del documento funcional. No filtra por empresa: de eso
    /// se encarga el filtro global de EF Core desde el claim (CLAUDE.md §5).
    /// </summary>
    /// <param name="busca">
    /// RFC o nombre del receptor. Si el texto es un número se interpreta además como folio,
    /// que es como lo busca un contador que trae la factura impresa en la mano.
    /// </param>
    /// <param name="desdeUtc">Inicio del rango, inclusivo. Ya en UTC.</param>
    /// <param name="hastaUtc">
    /// Fin del rango, <b>exclusivo</b> y ya en UTC. Quien llama traduce el día que eligió el
    /// usuario a instantes UTC: la columna guarda momentos, no fechas, y la tabla los muestra
    /// en la hora del lugar de expedición (CLAUDE.md §5). Si el corte se hiciera aquí sobre
    /// la fecha en UTC, una factura de las 8 de la noche aparecería fuera del día en que se
    /// emitió.
    /// </param>
    public async Task<PaginaDeComprobantes> ListarAsync(
        string? busca, EstatusComprobante? estatus, DateTime? desdeUtc, DateTime? hastaUtc,
        int pagina, int tamano, string? orden, bool descendente, CancellationToken ct)
    {
        var consulta = baseDeDatos.Comprobantes.AsNoTracking();

        if (estatus is { } valor)
        {
            var cadena = valor.ACadena();
            consulta = consulta.Where(c => c.Estatus == cadena);
        }

        if (desdeUtc is { } desde)
            consulta = consulta.Where(c => c.FechaEmisionUtc >= desde);

        if (hastaUtc is { } hasta)
            consulta = consulta.Where(c => c.FechaEmisionUtc < hasta);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var texto = busca.Trim();
            var folio = int.TryParse(texto, out var n) ? n : (int?)null;

            consulta = consulta.Where(c =>
                c.ReceptorRfc.Contains(texto) ||
                c.ReceptorNombre.Contains(texto) ||
                (folio != null && c.Folio == folio));
        }

        var total = await consulta.CountAsync(ct);

        consulta = Ordenar(consulta, orden, descendente);

        var elementos = await consulta
            .Skip(pagina * tamano)
            .Take(tamano)
            .Select(c => new ComprobanteEnListaDto(
                c.Id,
                c.Serie,
                c.Folio,
                c.FechaEmisionUtc,
                c.ReceptorRfc,
                c.ReceptorNombre,
                c.Total,
                EstatusComprobanteExtensiones.Desde(c.Estatus),
                c.Uuid))
            .ToListAsync(ct);

        return new PaginaDeComprobantes(elementos, total);
    }

    /// <summary>
    /// Lo más reciente primero por omisión: en una jornada de captura, el documento que se
    /// busca casi siempre es de hoy.
    /// </summary>
    private static IQueryable<Comprobante> Ordenar(
        IQueryable<Comprobante> consulta, string? orden, bool descendente) => orden switch
    {
        "folio" => descendente
            ? consulta.OrderByDescending(c => c.Folio)
            : consulta.OrderBy(c => c.Folio),
        "receptor" => descendente
            ? consulta.OrderByDescending(c => c.ReceptorNombre)
            : consulta.OrderBy(c => c.ReceptorNombre),
        "total" => descendente
            ? consulta.OrderByDescending(c => c.Total)
            : consulta.OrderBy(c => c.Total),
        "fecha" when !descendente => consulta.OrderBy(c => c.FechaEmisionUtc),
        _ => consulta.OrderByDescending(c => c.FechaEmisionUtc)
    };

    // ── Resolución de referencias ───────────────────────────────────────────────────────

    private async Task<Resultado<ReceptorFiscalDto?>> ResolverReceptorAsync(Guid? clienteId, CancellationToken ct)
    {
        if (clienteId is not { } id) return Resultado<ReceptorFiscalDto?>.Exito(null);

        var receptor = await clientes.ObtenerParaTimbradoAsync(id, ct);

        return receptor is null
            ? ErrorNegocio.Validacion("cliente-no-encontrado", "Ese cliente no existe o está dado de baja.")
            : Resultado<ReceptorFiscalDto?>.Exito(receptor);
    }

    private async Task<Resultado<IReadOnlyList<ConceptoResuelto>>> ResolverConceptosAsync(
        IReadOnlyList<ConceptoDto> conceptos, CancellationToken ct)
    {
        var resueltos = new List<ConceptoResuelto>(conceptos.Count);

        for (var i = 0; i < conceptos.Count; i++)
        {
            var linea = conceptos[i];

            if (linea.ProductoId is not { } productoId)
                return ErrorNegocio.Validacion(
                    "concepto-sin-producto",
                    $"El renglón {i + 1} no tiene un producto elegido.");

            var producto = await productos.ObtenerParaConceptoAsync(productoId, ct);

            if (producto is null)
                return ErrorNegocio.Validacion(
                    "producto-no-encontrado",
                    $"El producto del renglón {i + 1} no existe o está dado de baja.");

            resueltos.Add(new ConceptoResuelto(
                Orden: i + 1,
                Producto: producto,
                Cantidad: linea.Cantidad,
                Descuento: linea.Descuento,
                ACalcular: new ConceptoACalcular(
                    linea.Cantidad,
                    producto.ValorUnitario,
                    linea.Descuento,
                    producto.ObjetoImp,
                    [.. producto.Impuestos.Select(x =>
                        new ImpuestoDeConcepto(x.Impuesto, x.TipoFactor, x.TasaOCuota, x.EsRetencion))])));
        }

        return resueltos;
    }

    private sealed record ConceptoResuelto(
        int Orden, ProductoParaConceptoDto Producto, decimal Cantidad, decimal Descuento, ConceptoACalcular ACalcular);

    // ── Aplicar al comprobante ──────────────────────────────────────────────────────────

    private static void AplicarCabecera(
        Comprobante comprobante, PeticionGuardarBorrador peticion, ReceptorFiscalDto? receptor)
    {
        comprobante.SerieId = peticion.SerieId;
        comprobante.Moneda = peticion.Moneda;
        comprobante.TipoCambio = peticion.Moneda == "MXN" ? null : peticion.TipoCambio;
        comprobante.FormaPago = peticion.FormaPago;
        comprobante.MetodoPago = peticion.MetodoPago;
        comprobante.CondicionesDePago = peticion.CondicionesDePago;
        comprobante.Observaciones = peticion.Observaciones;

        comprobante.ClienteId = receptor?.ClienteId;
        comprobante.ReceptorRfc = receptor?.Rfc ?? string.Empty;
        comprobante.ReceptorNombre = receptor?.Nombre ?? string.Empty;
        comprobante.ReceptorRegimenFiscal = receptor?.RegimenFiscal ?? string.Empty;
        comprobante.ReceptorDomicilioFiscal = receptor?.DomicilioFiscalCp ?? string.Empty;
        comprobante.ReceptorUsoCfdi = peticion.ReceptorUsoCfdi ?? string.Empty;

        // Información Global solo aplica al RFC genérico nacional (CLAUDE.md §7); en
        // cualquier otro caso no se emite el nodo, así que ni se guardan los campos.
        var esPublicoEnGeneral = receptor?.Rfc == "XAXX010101000";

        comprobante.GlobalPeriodicidad = esPublicoEnGeneral ? peticion.GlobalPeriodicidad : null;
        comprobante.GlobalMeses = esPublicoEnGeneral ? peticion.GlobalMeses : null;
        comprobante.GlobalAnio = esPublicoEnGeneral ? peticion.GlobalAnio : null;
    }

    private static void AplicarConceptos(
        Comprobante comprobante, IReadOnlyList<ConceptoResuelto> resueltos, ComprobanteCalculado calculado)
    {
        comprobante.Conceptos.Clear();

        for (var i = 0; i < resueltos.Count; i++)
        {
            var origen = resueltos[i];
            var producto = origen.Producto;
            var calculo = calculado.Conceptos[i];

            var concepto = new Concepto
            {
                Id = Guid.NewGuid(),
                ComprobanteId = comprobante.Id,
                Orden = origen.Orden,
                ProductoId = producto.ProductoId,
                ClaveProdServ = producto.ClaveProdServ,
                ClaveUnidad = producto.ClaveUnidad,
                UnidadTexto = producto.UnidadTexto,
                Descripcion = producto.Descripcion,
                Cantidad = origen.Cantidad,
                ValorUnitario = producto.ValorUnitario,
                Importe = calculo.Importe,
                Descuento = calculo.Descuento,
                ObjetoImp = producto.ObjetoImp
            };

            foreach (var impuestoCalculado in calculo.Impuestos)
            {
                concepto.Impuestos.Add(new ImpuestoConcepto
                {
                    Id = Guid.NewGuid(),
                    ConceptoId = concepto.Id,
                    Impuesto = impuestoCalculado.Impuesto,
                    TipoFactor = impuestoCalculado.TipoFactor,
                    TasaOCuota = impuestoCalculado.TasaOCuota,
                    Base = impuestoCalculado.Base,
                    Importe = impuestoCalculado.Importe,
                    EsRetencion = impuestoCalculado.EsRetencion
                });
            }

            comprobante.Conceptos.Add(concepto);
        }
    }

    private static void AplicarRelacionados(
        Comprobante comprobante, IReadOnlyList<ComprobanteRelacionadoDto> relacionados)
    {
        comprobante.Relacionados.Clear();

        foreach (var relacionado in relacionados)
        {
            comprobante.Relacionados.Add(new ComprobanteRelacionado
            {
                Id = Guid.NewGuid(),
                ComprobanteId = comprobante.Id,
                TipoRelacion = relacionado.TipoRelacion,
                UuidRelacionado = relacionado.UuidRelacionado
            });
        }
    }

    private static void AplicarTotales(Comprobante comprobante, ComprobanteCalculado calculado)
    {
        comprobante.SubTotal = calculado.SubTotal;
        comprobante.Descuento = calculado.Descuento;
        comprobante.TotalImpuestosTrasladados = calculado.TotalImpuestosTrasladados;
        comprobante.TotalImpuestosRetenidos = calculado.TotalImpuestosRetenidos;
        comprobante.Total = calculado.Total;
    }

    // ── Lectura ─────────────────────────────────────────────────────────────────────────

    private async Task<Comprobante?> CargarAsync(Guid id, CancellationToken ct)
        => await baseDeDatos.Comprobantes
            .Include(c => c.Conceptos.OrderBy(x => x.Orden)).ThenInclude(x => x.Impuestos)
            .Include(c => c.Relacionados)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    private static ComprobanteDto ADto(Comprobante c) => new(
        c.Id, c.Estatus, c.TipoDeComprobante, c.SerieId, c.Serie, c.Folio,
        c.Moneda, c.TipoCambio, c.FormaPago, c.MetodoPago, c.Exportacion, c.CondicionesDePago,
        c.Observaciones,
        c.ClienteId,
        string.IsNullOrEmpty(c.ReceptorRfc) ? null : c.ReceptorRfc,
        string.IsNullOrEmpty(c.ReceptorNombre) ? null : c.ReceptorNombre,
        string.IsNullOrEmpty(c.ReceptorRegimenFiscal) ? null : c.ReceptorRegimenFiscal,
        string.IsNullOrEmpty(c.ReceptorDomicilioFiscal) ? null : c.ReceptorDomicilioFiscal,
        string.IsNullOrEmpty(c.ReceptorUsoCfdi) ? null : c.ReceptorUsoCfdi,
        c.GlobalPeriodicidad, c.GlobalMeses, c.GlobalAnio,
        c.SubTotal, c.Descuento, c.TotalImpuestosTrasladados, c.TotalImpuestosRetenidos, c.Total,
        c.Uuid, c.FechaTimbradoUtc,
        [.. c.Conceptos.Select(x => new ConceptoDto(
            x.Id, x.Orden, x.ProductoId, x.ClaveProdServ, x.ClaveUnidad, x.UnidadTexto, x.NoIdentificacion,
            x.Descripcion, x.Cantidad, x.ValorUnitario, x.Importe, x.Descuento, x.ObjetoImp,
            [.. x.Impuestos.Select(i => new ImpuestoDeConceptoDto(i.Impuesto, i.TipoFactor, i.TasaOCuota, i.EsRetencion))]))],
        [.. c.Relacionados.Select(r => new ComprobanteRelacionadoDto(r.Id, r.TipoRelacion, r.UuidRelacionado, null, null))]);
}
