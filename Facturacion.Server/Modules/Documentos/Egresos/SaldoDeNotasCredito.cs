using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Modules.Documentos.Pagos;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Egresos;

internal static class SaldoDeNotasCredito
{
    internal static async Task<Resultado<DesgloseDeFacturaParaPago>> CalcularDesgloseAsync(AppDbContext db,
        Comprobante factura, decimal abono, CancellationToken ct, Guid? excluir = null, Guid? actual = null)
    {
        if (!CalculoDeNotaCredito.TieneTratamientosMixtos(factura) || !await db.Comprobantes.AnyAsync(c =>
            c.TipoDeComprobante == "E" && (c.Estatus == "timbrando" || c.Estatus == "timbrado" || c.Estatus == "en_cancelacion") &&
            c.Relacionados.Any(r => r.UuidRelacionado == factura.Uuid), ct))
            return DesgloseDeFacturaParaPago.Calcular(factura, abono);
        var neta = await ObtenerFacturaNetaAsync(db, factura, ct);
        if (neta.EsFallo) return neta.Error!;
        var saldo = SaldoFiscalParaPago.Calcular(neta.Valor, await PagosActivosAsync(db, factura.Uuid!.Value, ct, excluir, actual));
        return saldo.EsFallo ? saldo.Error! : saldo.Valor.CalcularAbono(abono);
    }

    internal static Task<List<DocumentoPagado>> PagosActivosAsync(AppDbContext db, Guid factura, CancellationToken ct,
        Guid? excluir = null, Guid? actual = null)
        => db.PagosDocumentos.AsNoTracking().Include(d => d.Impuestos).Where(d => d.IdDocumento == factura &&
            d.Pago.ComprobanteId != excluir && d.Pago.ComprobanteId != actual &&
            (d.Pago.Comprobante.Estatus == "timbrando" || d.Pago.Comprobante.Estatus == "timbrado" ||
             d.Pago.Comprobante.Estatus == "en_cancelacion")).ToListAsync(ct);

    internal static async Task<Resultado> ValidarNotaMixtaAsync(AppDbContext db, Comprobante origen, Comprobante nota, CancellationToken ct)
    {
        var notas = await db.Comprobantes.AsNoTracking().Include(c => c.Relacionados)
            .Include(c => c.Conceptos).ThenInclude(c => c.Impuestos)
            .Where(c => c.Id != nota.Id && c.TipoDeComprobante == "E" &&
                (c.Estatus == "timbrando" || c.Estatus == "timbrado" || c.Estatus == "en_cancelacion") &&
                c.Relacionados.Any(r => r.UuidRelacionado == origen.Uuid)).ToListAsync(ct);
        notas.Add(nota);
        var neta = FacturaNetaParaPago.Calcular(origen, notas);
        if (neta.EsFallo) return neta.Error!;
        var saldo = SaldoFiscalParaPago.Calcular(neta.Valor, await PagosActivosAsync(db, origen.Uuid!.Value, ct));
        return saldo.EsFallo ? saldo.Error! : Resultado.Exito();
    }

    internal static async Task<Resultado<Comprobante>> ObtenerFacturaNetaAsync(AppDbContext db, Comprobante factura, CancellationToken ct)
    {
        if (!CalculoDeNotaCredito.TieneTratamientosMixtos(factura)) return factura;
        var notas = await db.Comprobantes.AsNoTracking().Include(c => c.Relacionados)
            .Include(c => c.Conceptos).ThenInclude(c => c.Impuestos)
            .Where(c => c.TipoDeComprobante == "E" &&
                (c.Estatus == "timbrando" || c.Estatus == "timbrado" || c.Estatus == "en_cancelacion") &&
                c.Relacionados.Any(r => r.UuidRelacionado == factura.Uuid)).ToListAsync(ct);
        return notas.Count == 0 ? factura : FacturaNetaParaPago.Calcular(factura, notas);
    }

    internal static async Task<Resultado<decimal>> ObtenerAsync(AppDbContext db, Guid factura, CancellationToken ct)
    {
        var notas = await db.Comprobantes.AsNoTracking()
            .Where(c => c.TipoDeComprobante == "E" &&
                (c.Estatus == "timbrando" || c.Estatus == "timbrado" || c.Estatus == "en_cancelacion") &&
                c.Relacionados.Any(r => r.UuidRelacionado == factura))
            .Select(c => new { c.Total, c.Moneda, c.Variante, c.Estatus, Relaciones = c.Relacionados.Count,
                RelacionValida = c.Relacionados.All(r => r.TipoRelacion == "01" || r.TipoRelacion == "03") }).ToListAsync(ct);
        if (notas.Any(n => n.Moneda != "MXN" || n.Variante != CalculoDeNotaCredito.Variante ||
            n.Relaciones != 1 || !n.RelacionValida || n.Total <= 0))
            return ErrorNegocio.Conflicto("egresos-no-compatibles", "La factura tiene egresos de otra modalidad. Revisa sus saldos antes de registrar un pago.");
        if (notas.Any(n => n.Estatus is "timbrando" or "en_cancelacion"))
            return ErrorNegocio.Conflicto("nota-en-proceso", "Espera a que termine la emisión o cancelación de las notas de esta factura antes de registrar su pago.");
        return notas.Sum(n => n.Total);
    }

    internal static async Task<decimal> AbonadoAsync(AppDbContext db, Guid factura, CancellationToken ct,
        Guid? excluir = null, Guid? excluirActual = null)
        => await db.PagosDocumentos.Where(d => d.IdDocumento == factura &&
            d.Pago.ComprobanteId != excluir && d.Pago.ComprobanteId != excluirActual &&
            (d.Pago.Comprobante.Estatus == "timbrando" || d.Pago.Comprobante.Estatus == "timbrado" ||
             d.Pago.Comprobante.Estatus == "en_cancelacion"))
            .SumAsync(d => (decimal?)d.ImpPagado, ct) ?? 0m;
}
