using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Pagos;

internal static class SustitucionDePago
{
    internal static async Task<bool> TienePendienteAsync(AppDbContext db, Guid documento, CancellationToken ct)
        => await db.Comprobantes.AnyAsync(c => c.TipoDeComprobante == "P" &&
            (c.Estatus == "timbrando" || c.Estatus == "timbrado" || c.Estatus == "en_cancelacion") &&
            c.Relacionados.Any(r => r.TipoRelacion == "04" && db.Comprobantes.Any(o =>
                o.Uuid == r.UuidRelacionado && o.Estatus != "cancelado" &&
                o.Pagos.Any(p => p.Documentos.Any(d => d.IdDocumento == documento)))), ct);

    internal static async Task<Resultado<Comprobante?>> ResolverAsync(
        AppDbContext db, Guid? uuid, Guid actual, Guid? cliente, CancellationToken ct)
    {
        if (uuid is null) return Resultado<Comprobante?>.Exito(null);
        if (uuid == Guid.Empty)
            return ErrorNegocio.Validacion("sustitucion-pago-invalida", "Escribe un folio fiscal válido.");
        var origen = await db.Comprobantes.AsNoTracking()
            .Include(c => c.Relacionados)
            .Include(c => c.Pagos).ThenInclude(p => p.Documentos).ThenInclude(d => d.Impuestos)
            .SingleOrDefaultAsync(c => c.Uuid == uuid && c.TipoDeComprobante == "P", ct);
        if (origen is null || origen.Id == actual || origen.Estatus is not ("timbrado" or "cancelado") ||
            origen.Pagos.Count != 1 || origen.Pagos[0].Documentos.Count == 0)
            return ErrorNegocio.Validacion("sustitucion-pago-invalida", "El folio debe corresponder a otro pago emitido de esta empresa.");
        if (cliente is { } id && origen.ClienteId != id)
            return ErrorNegocio.Validacion("sustitucion-otro-cliente", "El pago anterior debe corresponder al mismo cliente.");
        if (await db.Comprobantes.AnyAsync(c => c.Id != actual &&
            (c.Estatus == "timbrando" || c.Estatus == "timbrado" || c.Estatus == "en_cancelacion") &&
            c.Relacionados.Any(r => r.TipoRelacion == "04" && r.UuidRelacionado == uuid), ct))
            return ErrorNegocio.Conflicto("pago-ya-sustituido", "Ese pago ya tiene un sustituto emitido o en proceso.");
        foreach (var documento in origen.Pagos[0].Documentos)
        {
            var egresos = await Egresos.SaldoDeNotasCredito.ObtenerAsync(db, documento.IdDocumento, ct);
            if (egresos.EsFallo) return egresos.Error!;
            if (await TienePendienteAsync(db, documento.IdDocumento, ct))
                return ErrorNegocio.Conflicto("sustitucion-pago-pendiente", "Termina la cancelación del pago anterior antes de registrar otro pago.");
            if (await db.PagosDocumentos.AnyAsync(d => d.Pago.ComprobanteId != origen.Id &&
                d.Pago.ComprobanteId != actual && d.IdDocumento == documento.IdDocumento &&
                (d.NumParcialidad > documento.NumParcialidad ||
                 (d.NumParcialidad == documento.NumParcialidad && d.Pago.Comprobante.Estatus != "cancelado")) &&
                (d.Pago.Comprobante.Estatus == "timbrando" || d.Pago.Comprobante.Estatus == "timbrado" ||
                 d.Pago.Comprobante.Estatus == "en_cancelacion" || d.Pago.Comprobante.Estatus == "cancelado"), ct))
                return ErrorNegocio.Conflicto("pago-con-parcialidades-posteriores", "Hay pagos posteriores. Esta sustitución solo admite el último pago de las facturas.");
            var tieneHistorialDeNotas = egresos.Valor > 0 || await db.Comprobantes.AnyAsync(c =>
                c.TipoDeComprobante == "E" && c.Estatus == "cancelado" && c.Uuid != null &&
                c.Relacionados.Any(r => r.UuidRelacionado == documento.IdDocumento), ct);
            if (tieneHistorialDeNotas && await ValidarEgresosAsync(db, documento, origen.Id, actual, egresos.Valor, ct) is { } errorEgresos)
                return errorEgresos;
        }
        return Resultado<Comprobante?>.Exito(origen);
    }

    private static async Task<ErrorNegocio?> ValidarEgresosAsync(AppDbContext db, DocumentoPagado documento,
        Guid origen, Guid actual, decimal totalNotas, CancellationToken ct)
    {
        var factura = await db.Comprobantes.AsNoTracking().Include(c => c.Conceptos).ThenInclude(c => c.Impuestos)
            .SingleOrDefaultAsync(c => c.Uuid == documento.IdDocumento && c.TipoDeComprobante == "I", ct);
        if (factura is null || factura.Estatus != "timbrado" || factura.MetodoPago != "PPD" || factura.Moneda != "MXN")
            return ErrorNegocio.Conflicto("sustitucion-origen-no-admitido", "Revisa la factura PPD y sus notas antes de sustituir el pago.");
        var otrosAbonos = await Egresos.SaldoDeNotasCredito.AbonadoAsync(db, documento.IdDocumento, ct, origen, actual);
        // El pago sustituido se excluye una vez; las notas se descuentan del total de la factura.
        if (factura.Total - totalNotas - otrosAbonos != documento.ImpSaldoAnt || documento.MonedaDR != factura.Moneda)
            return SaldoConEgresosDistinto();
        var desglose = await Egresos.SaldoDeNotasCredito.CalcularDesgloseAsync(db, factura, documento.ImpPagado, ct, origen, actual);
        if (desglose.EsFallo) return desglose.Error!;
        return desglose.Valor.CoincideCon(documento) ? null : SaldoConEgresosDistinto();
    }

    private static ErrorNegocio SaldoConEgresosDistinto() => ErrorNegocio.Conflicto("sustitucion-egresos-desactualizados",
        "Las notas o los abonos cambiaron el saldo o los impuestos del pago anterior. Revisa el historial antes de sustituirlo.");

    internal static async Task<ErrorNegocio?> ValidarCancelacionAsync(
        AppDbContext db, Comprobante pago, string motivo, Guid? sustituto, CancellationToken ct)
    {
        if (pago.TipoDeComprobante != "P") return null;
        var emitidos = db.Comprobantes.Where(c => c.TipoDeComprobante == "P" &&
            (c.Estatus == "timbrando" || c.Estatus == "timbrado" || c.Estatus == "en_cancelacion"));
        var reemplazo = await emitidos.Where(c => c.Relacionados.Any(r => r.TipoRelacion == "04" &&
            r.UuidRelacionado == pago.Uuid)).Select(c => new { c.Uuid, c.Estatus }).FirstOrDefaultAsync(ct);
        if (motivo == "01" && (reemplazo is null || reemplazo.Estatus != "timbrado" || reemplazo.Uuid != sustituto))
            return ErrorNegocio.Validacion("sustituto-pago-no-relacionado", "El sustituto debe ser un pago timbrado que relacione este folio con clave 04.");
        if (reemplazo is not null && (motivo != "01" || sustituto != reemplazo.Uuid || reemplazo.Estatus != "timbrado"))
            return ErrorNegocio.Conflicto("cancelacion-pago-sustituido", "Cancela con motivo 01 y el folio del pago sustituto ya timbrado.");
        if (await db.Comprobantes.AnyAsync(o => o.Estatus != "cancelado" &&
            db.Comprobantes.Any(c => c.Id == pago.Id && c.Relacionados.Any(r =>
                r.TipoRelacion == "04" && r.UuidRelacionado == o.Uuid)), ct))
            return ErrorNegocio.Conflicto("cancelacion-origen-pendiente", "Primero termina la cancelación del pago que este comprobante sustituye.");
        var documentos = await db.PagosDocumentos.Where(d => d.Pago.ComprobanteId == pago.Id).ToListAsync(ct);
        foreach (var d in documentos)
            if (await db.PagosDocumentos.AnyAsync(p => p.IdDocumento == d.IdDocumento &&
                p.NumParcialidad > d.NumParcialidad && p.Pago.ComprobanteId != pago.Id &&
                (p.Pago.Comprobante.Estatus == "timbrando" || p.Pago.Comprobante.Estatus == "timbrado" ||
                 p.Pago.Comprobante.Estatus == "en_cancelacion"), ct))
                return ErrorNegocio.Conflicto("pago-con-parcialidades-posteriores", "Cancela primero los pagos posteriores de estas facturas.");
        return null;
    }
}
