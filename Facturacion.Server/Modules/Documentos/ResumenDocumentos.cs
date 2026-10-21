using Facturacion.Server.Data;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos;

/// <summary>Resumen de la empresa activa para el tablero. Interpreta las fechas en el huso de expedición e incluye ambos extremos del periodo; EF aplica el filtro de empresa.</summary>
public sealed class ResumenDocumentos(AppDbContext baseDeDatos, HusoDeEmpresa huso)
    : IResumenDocumentos
{
    public async Task<ResumenDocumentosDto> ObtenerAsync(
        DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var (inicioUtc, finUtc) = await huso.ARangoUtcAsync(desde, hasta, ct);

        var porEstatus = await baseDeDatos.Comprobantes
            .AsNoTracking()
            .Where(c => c.FechaEmisionUtc >= inicioUtc && c.FechaEmisionUtc <= finUtc)
            .GroupBy(c => c.Estatus)
            .Select(g => new { Estatus = g.Key, Cuenta = g.Count(), Importe = g.Sum(c => c.Total) })
            .ToListAsync(ct);

        var conteo = porEstatus.ToDictionary(
            x => EstatusComprobanteExtensiones.Desde(x.Estatus),
            x => x.Cuenta);

        return new ResumenDocumentosDto(
            conteo, Importe(EstatusComprobante.Timbrado), Importe(EstatusComprobante.Cancelado));

        decimal Importe(EstatusComprobante estatus)
            => porEstatus.Where(x => x.Estatus == estatus.ACadena()).Sum(x => x.Importe);
    }

    private static readonly string[] NombresDeMes =
        ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    public async Task<IReadOnlyList<PuntoMensualDeDocumentosDto>> SerieMensualAsync(
        int meses, CancellationToken ct)
    {
        meses = Math.Clamp(meses, 1, 24);

        // El rango se arma en el huso de la empresa por lo mismo que el resumen: el mes del
        // contador, no el de UTC. Se pide desde el primer dia del mes mas antiguo hasta hoy.
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var primerMes = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-(meses - 1));

        var (inicioUtc, finUtc) = await huso.ARangoUtcAsync(primerMes, hoy, ct);

        var timbrado = EstatusComprobante.Timbrado.ACadena();

        var agrupado = await baseDeDatos.Comprobantes
            .AsNoTracking()
            .Where(c => c.Estatus == timbrado
                        && c.FechaEmisionUtc >= inicioUtc
                        && c.FechaEmisionUtc <= finUtc)
            .GroupBy(c => new { c.FechaEmisionUtc.Year, c.FechaEmisionUtc.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Cuenta = g.Count(),
                Importe = g.Sum(c => c.Total)
            })
            .ToListAsync(ct);

        // Los meses sin actividad salen en cero y no ausentes: una serie que se salta los
        // meses vacios miente sobre la forma de la curva.
        return Enumerable.Range(0, meses)
            .Select(i =>
            {
                var mes = primerMes.AddMonths(i);
                var dato = agrupado.FirstOrDefault(a => a.Year == mes.Year && a.Month == mes.Month);

                return new PuntoMensualDeDocumentosDto(
                    $"{NombresDeMes[mes.Month - 1]} {mes.Year % 100:00}",
                    dato?.Cuenta ?? 0,
                    dato?.Importe ?? 0m);
            })
            .ToList();
    }
}
