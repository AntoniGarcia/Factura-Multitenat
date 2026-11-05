using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Documentos.Emision;
using Facturacion.Server.Modules.Documentos.Impuestos;
using Facturacion.Server.Modules.Documentos.Pagos;
using Facturacion.Server.Modules.Documentos.Salidas;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Facturacion.Shared.Documentos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Egresos;

public sealed class ServicioDeNotasCredito(AppDbContext db, IContextoEmpresaInterno contexto,
    IServicioEmpresaEmisora emisora, IServicioFolios folios, IServicioCatalogosSat catalogos,
    ServicioDeEmision emision, IServicioDeBitacora bitacora)
{
    public async Task<Resultado<OrigenNotaCreditoDto>> ConsultarOrigenAsync(string serie, int folio, CancellationToken ct)
    {
        if (ExigirCaptura() is { } sinPermiso) return sinPermiso;
        var origen = await db.Comprobantes.AsNoTracking().Include(c => c.Conceptos).ThenInclude(c => c.Impuestos)
            .SingleOrDefaultAsync(c => c.Serie == serie && c.Folio == folio && c.TipoDeComprobante == "I", ct);
        if (origen is null) return NoExisteOrigen();
        if (CalculoDeNotaCredito.ValidarOrigen(origen) is { } invalido) return invalido;
        var consumido = await AcreditadoAsync(origen.Uuid!.Value, Guid.Empty, ct);
        if (consumido.EsFallo) return consumido.Error!;
        return await AOrigenAsync(origen, consumido.Valor.PorConcepto, consumido.Valor.Total, ct);
    }

    public async Task<Resultado<NotaCreditoDto>> CrearAsync(Guid uuid, CancellationToken ct)
    {
        if (PermisoDeEmision.Exigir(contexto, Permisos.FacturaCrear) is { } sinPermiso) return sinPermiso;
        var origen = await CargarOrigenAsync(uuid, ct);
        if (origen is null) return NoExisteOrigen();
        if (CalculoDeNotaCredito.ValidarOrigen(origen) is { } invalido) return invalido;
        var emisor = await emisora.ObtenerParaTimbradoAsync(ct);
        if (origen.EmisorRfc != emisor.Rfc)
            return ErrorNegocio.Validacion("emisor-nota-distinto", "El RFC actual de la empresa no coincide con el de la factura.");
        var nota = new Comprobante
        {
            Id = Guid.NewGuid(), TipoDeComprobante = "E", Variante = CalculoDeNotaCredito.Variante,
            Estatus = "borrador", FechaEmisionUtc = DateTime.UtcNow, CreadoUtc = DateTime.UtcNow, ModificadoUtc = DateTime.UtcNow,
            CreadoPorUsuarioId = contexto.UsuarioActual ?? Guid.Empty,
            EmisorRfc = emisor.Rfc, EmisorNombre = emisor.Nombre, EmisorRegimenFiscal = emisor.RegimenFiscal,
            LugarExpedicion = emisor.CodigoPostalExpedicion, Moneda = "MXN", Exportacion = "01", MetodoPago = "PUE",
            ClienteId = origen.ClienteId, ReceptorRfc = origen.ReceptorRfc, ReceptorNombre = origen.ReceptorNombre,
            ReceptorRegimenFiscal = origen.ReceptorRegimenFiscal, ReceptorDomicilioFiscal = origen.ReceptorDomicilioFiscal,
            ReceptorUsoCfdi = "G02",
            Relacionados = [new() { Id = Guid.NewGuid(), TipoRelacion = "01", UuidRelacionado = uuid }]
        };
        db.Comprobantes.Add(nota);
        bitacora.Registrar(EntidadesDeBitacora.Comprobante, nota.Id.ToString(), AccionesDeBitacora.ComprobanteGuardado,
            antes: null, despues: new { nota.TipoDeComprobante, nota.ClienteId, nota.ReceptorRfc, FacturaOriginal = uuid });
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(nota.Id, ct);
    }

    public async Task<Resultado<NotaCreditoDto>> ObtenerAsync(Guid id, CancellationToken ct)
    {
        if (ExigirCaptura() is { } sinPermiso) return sinPermiso;
        var nota = await CargarNotaAsync(id, ct);
        if (nota is null) return NoExisteNota();
        if (nota.Relacionados.Count != 1) return ErrorNegocio.Validacion("origen-nota-invalido", "La nota debe relacionar una factura.");
        var origen = await CargarOrigenAsync(nota.Relacionados[0].UuidRelacionado, ct);
        if (origen is null) return NoExisteOrigen();
        var consumido = await AcreditadoAsync(origen.Uuid!.Value, id, ct);
        if (consumido.EsFallo) return consumido.Error!;
        return new NotaCreditoDto((await emision.ObtenerAsync(id, ct))!, await AOrigenAsync(origen, consumido.Valor.PorConcepto, consumido.Valor.Total, ct),
            nota.Estatus is ("borrador" or "error") && PermisoDeEmision.ExigirEdicion(contexto, nota) is null);
    }

    public async Task<Resultado<NotaCreditoDto>> GuardarAsync(Guid id, PeticionGuardarNotaCredito peticion, CancellationToken ct)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        await BloqueoDePagos.TomarAsync(db, contexto.EmpresaActual ?? throw new InvalidOperationException("Falta empresa activa."), ct);
        var nota = await CargarNotaAsync(id, ct);
        if (nota is null) return NoExisteNota();
        if (PermisoDeEmision.ExigirEdicion(contexto, nota) is { } sinPermiso) return sinPermiso;
        if (nota.Estatus is not ("borrador" or "error"))
            return ErrorNegocio.Conflicto("nota-no-editable", "Solo se puede modificar una nota en borrador o error.");
        if (nota.Relacionados.Count != 1) return ErrorNegocio.Validacion("origen-nota-invalido", "La nota debe relacionar una factura.");
        var origen = await CargarOrigenAsync(nota.Relacionados[0].UuidRelacionado, ct);
        if (origen is null) return NoExisteOrigen();
        if (await ValidarCapturaAsync(peticion, origen, ct) is { } invalido) return invalido;
        var consumido = await AcreditadoAsync(origen.Uuid!.Value, id, ct);
        if (consumido.EsFallo) return consumido.Error!;
        var calculado = CalculoDeNotaCredito.Calcular(origen, peticion, consumido.Valor.PorConcepto, consumido.Valor.IvaPorConcepto);
        if (calculado.EsFallo) return calculado.Error!;
        var antes = await emision.ObtenerAsync(id, ct);
        db.Conceptos.RemoveRange(nota.Conceptos);
        nota.Conceptos.Clear(); nota.Conceptos.AddRange(calculado.Valor);
        nota.SerieId = peticion.SerieId; nota.FormaPago = peticion.FormaPago;
        nota.Observaciones = string.IsNullOrWhiteSpace(peticion.Observaciones) ? null : peticion.Observaciones.Trim();
        nota.Relacionados[0].TipoRelacion = peticion.TipoRelacion;
        var proyeccion = ProyeccionMonetariaCfdi.Calcular(nota, 2, null);
        if (proyeccion.Total <= 0 || proyeccion.Total + consumido.Valor.Total > origen.Total)
            return ErrorNegocio.Validacion("total-nota-invalido", "El total debe ser positivo y no superar lo facturado después de otras notas.");
        proyeccion.AplicarA(nota);
        if (await ValidarSaldoPpdAsync(origen, nota, consumido.Valor.Total, ct) is { } sinSaldo) return sinSaldo;
        nota.ModificadoUtc = DateTime.UtcNow;
        bitacora.Registrar(EntidadesDeBitacora.Comprobante, nota.Id.ToString(), AccionesDeBitacora.ComprobanteGuardado,
            antes, new { nota.TipoDeComprobante, nota.Total, peticion });
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    internal async Task<Resultado> ValidarParaEmisionAsync(Comprobante nota, CancellationToken ct)
    {
        if (nota.Variante != CalculoDeNotaCredito.Variante || nota.Relacionados.Count != 1 ||
            nota.TipoDeComprobante != "E" || nota.Moneda != "MXN" || nota.Exportacion != "01" ||
            nota.MetodoPago != "PUE" || nota.TipoCambio is not null || nota.TasaIsh is not null ||
            nota.GlobalPeriodicidad is not null || nota.ReceptorUsoCfdi != "G02")
            return ErrorNegocio.Validacion("nota-no-admitida", "La nota de crédito contiene datos fuera de su modalidad de captura.");
        var origen = await CargarOrigenAsync(nota.Relacionados[0].UuidRelacionado, ct);
        if (origen is null) return NoExisteOrigen();
        if (nota.EmisorRfc != origen.EmisorRfc || nota.ReceptorRfc != origen.ReceptorRfc || nota.ClienteId != origen.ClienteId)
            return ErrorNegocio.Validacion("receptor-nota-distinto", "La nota debe conservar el emisor y receptor de la factura relacionada.");
        var peticion = new PeticionGuardarNotaCredito(nota.SerieId, nota.FormaPago ?? "", nota.Relacionados[0].TipoRelacion,
            nota.Observaciones, nota.Conceptos.Select(c => new RenglonNotaCreditoDto(c.Orden, c.Importe, c.Descripcion)).ToArray());
        if (await ValidarCapturaAsync(peticion, origen, ct) is { } invalido) return invalido;
        var consumido = await AcreditadoAsync(origen.Uuid!.Value, nota.Id, ct);
        if (consumido.EsFallo) return consumido.Error!;
        var calculado = CalculoDeNotaCredito.Calcular(origen, peticion, consumido.Valor.PorConcepto, consumido.Valor.IvaPorConcepto);
        if (calculado.EsFallo) return calculado.Error!;
        if (await ValidarSaldoPpdAsync(origen, nota, consumido.Valor.Total, ct) is { } sinSaldo) return sinSaldo;
        var actual = nota.Conceptos.OrderBy(c => c.Orden).ToArray();
        var esperado = calculado.Valor.OrderBy(c => c.Orden).ToArray();
        if (actual.Length != esperado.Length || actual.Zip(esperado).Any(par => !Coinciden(par.First, par.Second)) ||
            !ProyeccionMonetariaCfdi.Calcular(nota, 2, null).CoincideCon(nota) ||
            nota.Total <= 0 || nota.Total + consumido.Valor.Total > origen.Total)
            return ErrorNegocio.Conflicto("nota-desactualizada", "Guarda de nuevo la nota: sus conceptos o importes ya no coinciden con lo disponible.");
        return Resultado.Exito();
    }

    private async Task<ErrorNegocio?> ValidarCapturaAsync(PeticionGuardarNotaCredito peticion, Comprobante origen, CancellationToken ct)
    {
        if (CalculoDeNotaCredito.ValidarOrigen(origen) is { } error) return error;
        var emisor = await emisora.ObtenerParaTimbradoAsync(ct);
        if (origen.EmisorRfc != emisor.Rfc)
            return ErrorNegocio.Validacion("emisor-nota-distinto", "El RFC actual de la empresa no coincide con el de la factura.");
        if (peticion.SerieId is not { } serie) return ErrorNegocio.Validacion("serie-nota-requerida", "Selecciona una serie activa de tipo Egreso.");
        var valida = await folios.ValidarSerieAsync(serie, "E", ct);
        if (valida.EsFallo) return valida.Error!;
        if (peticion.FormaPago is "99" or "30" || !await db.SatFormasPago.AnyAsync(f => f.Clave == peticion.FormaPago && f.Vigente, ct))
            return ErrorNegocio.Validacion("forma-pago-nota-invalida", "Selecciona la forma de devolución o bonificación; no aplica por definir ni anticipo.");
        if (peticion.Observaciones?.Length > 1000)
            return ErrorNegocio.Validacion("observaciones-nota-largas", "Las observaciones admiten hasta 1000 caracteres.");
        if (!await catalogos.EsUsoCfdiCompatibleAsync("G02", origen.ReceptorRegimenFiscal, origen.ReceptorRfc.Length == 12, ct))
            return ErrorNegocio.Validacion("uso-nota-incompatible", "El régimen del receptor no admite G02. Revisa sus datos fiscales.");
        return null;
    }

    private async Task<Resultado<(Dictionary<int, decimal> PorConcepto, Dictionary<int, decimal> IvaPorConcepto, decimal Total)>> AcreditadoAsync(Guid uuid, Guid excluir, CancellationToken ct)
    {
        var notas = await db.Comprobantes.AsNoTracking().Include(c => c.Conceptos).ThenInclude(c => c.Impuestos)
            .Where(c => c.Id != excluir && c.TipoDeComprobante == "E" &&
                (c.Estatus == "timbrando" || c.Estatus == "timbrado" || c.Estatus == "en_cancelacion") &&
                c.Relacionados.Any(r => r.UuidRelacionado == uuid)).ToListAsync(ct);
        if (notas.Any(c => c.Variante != CalculoDeNotaCredito.Variante))
            return ErrorNegocio.Conflicto("notas-previas-no-compatibles", "La factura tiene egresos de otra modalidad que requieren revisión.");
        return (notas.SelectMany(c => c.Conceptos).GroupBy(c => c.Orden).ToDictionary(g => g.Key, g => g.Sum(c => c.Importe - c.Descuento)),
            notas.SelectMany(c => c.Conceptos).GroupBy(c => c.Orden).ToDictionary(g => g.Key,
                g => g.Sum(c => c.Impuestos.Sum(i => Math.Round(i.Importe ?? 0m, 2, MotorDeImpuestos.ModoDeRedondeo)))),
            notas.Sum(c => c.Total));
    }

    private Task<Comprobante?> CargarNotaAsync(Guid id, CancellationToken ct)
        => db.Comprobantes.Include(c => c.Conceptos).ThenInclude(c => c.Impuestos).Include(c => c.Relacionados)
            .SingleOrDefaultAsync(c => c.Id == id && c.TipoDeComprobante == "E" && c.Variante == CalculoDeNotaCredito.Variante, ct);
    private Task<Comprobante?> CargarOrigenAsync(Guid uuid, CancellationToken ct)
        => db.Comprobantes.AsNoTracking().Include(c => c.Conceptos).ThenInclude(c => c.Impuestos)
            .SingleOrDefaultAsync(c => c.Uuid == uuid && c.TipoDeComprobante == "I", ct);
    private async Task<OrigenNotaCreditoDto> AOrigenAsync(Comprobante c, IReadOnlyDictionary<int, decimal> consumido, decimal totalConsumido, CancellationToken ct)
        => new(c.Uuid!.Value, c.Serie, c.Folio, c.ReceptorRfc, c.ReceptorNombre, c.Moneda, c.Total,
            c.Conceptos.OrderBy(x => x.Orden).Select(x => new ConceptoAcreditableDto(x.Orden, x.Descripcion,
                x.Importe - x.Descuento, Math.Max(0, x.Importe - x.Descuento - consumido.GetValueOrDefault(x.Orden)))).ToArray())
        {
            SaldoPendiente = c.MetodoPago == "PPD" ? Math.Max(0, c.Total - totalConsumido -
                await SaldoDeNotasCredito.AbonadoAsync(db, c.Uuid.Value, ct)) : null
        };
    private async Task<ErrorNegocio?> ValidarSaldoPpdAsync(Comprobante origen, Comprobante nota, decimal otrasNotas, CancellationToken ct)
    {
        if (origen.MetodoPago != "PPD") return null;
        if (await SustitucionDePago.TienePendienteAsync(db, origen.Uuid!.Value, ct))
            return ErrorNegocio.Conflicto("sustitucion-pago-pendiente", "Termina la sustitución del pago antes de acreditar esta factura.");
        var abonado = await SaldoDeNotasCredito.AbonadoAsync(db, origen.Uuid!.Value, ct);
        if (nota.Total + otrasNotas + abonado > origen.Total)
            return ErrorNegocio.Conflicto("nota-supera-saldo-ppd", "La nota supera el saldo pendiente después de los pagos y otras notas vigentes.");
        if (CalculoDeNotaCredito.TieneTratamientosMixtos(origen))
        {
            var valido = await SaldoDeNotasCredito.ValidarNotaMixtaAsync(db, origen, nota, ct);
            if (valido.EsFallo) return valido.Error!;
        }
        return null;
    }
    private static bool Coinciden(Concepto a, Concepto b)
        => a.Orden == b.Orden && a.ClaveProdServ == b.ClaveProdServ && a.ClaveUnidad == b.ClaveUnidad &&
            a.Descripcion == b.Descripcion && a.Cantidad == b.Cantidad && a.ValorUnitario == b.ValorUnitario &&
            a.Importe == b.Importe && a.Descuento == b.Descuento && a.ObjetoImp == b.ObjetoImp &&
            a.AplicaIsh == b.AplicaIsh && a.UnidadTexto == b.UnidadTexto && a.NoIdentificacion == b.NoIdentificacion &&
            a.ProductoId == b.ProductoId && a.DatosFiscalesJson == b.DatosFiscalesJson && Impuestos(a).SequenceEqual(Impuestos(b));
    private static IEnumerable<(string Impuesto, string Factor, decimal? Tasa, decimal Base, decimal? Importe, bool Retencion)> Impuestos(Concepto c)
        => c.Impuestos.OrderBy(i => i.Impuesto, StringComparer.Ordinal).ThenBy(i => i.TipoFactor, StringComparer.Ordinal)
            .ThenBy(i => i.TasaOCuota).ThenBy(i => i.EsRetencion)
            .Select(i => (i.Impuesto, i.TipoFactor, i.TasaOCuota, i.Base, i.Importe, i.EsRetencion));
    private ErrorNegocio? ExigirCaptura() => contexto.Tiene(Permisos.FacturaCrear) || contexto.Tiene(Permisos.FacturaModificar)
        ? null : ErrorNegocio.SinPermiso("sin-permiso-nota", "No tienes permiso para capturar notas de crédito.");
    private static ErrorNegocio NoExisteOrigen() => ErrorNegocio.NoEncontrado("factura-origen-no-encontrada", "No se encontró la factura original en esta empresa.");
    private static ErrorNegocio NoExisteNota() => ErrorNegocio.NoEncontrado("nota-no-encontrada", "No se encontró la nota de crédito en esta empresa.");
}
