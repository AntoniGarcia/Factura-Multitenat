using Facturacion.Server.Data;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos;

/// <summary>
/// Lo único que la mitad A consume de la mitad B: las cifras del tablero.
///
/// <para><b>Qué huso usan las fechas del contrato</b></para>
/// <see cref="IResumenDocumentos.ObtenerAsync"/> recibe <c>DateOnly</c>, que no lleva huso,
/// y los comprobantes se guardan en UTC. Se interpretan como <b>fechas de calendario en el
/// huso del lugar de expedición de la empresa</b> (CLAUDE.md §5): «agosto» es el agosto del
/// contador, no el de UTC. El contrato no lo dice; se decidió aquí y quedó anotado como algo
/// que le faltaba. Los dos extremos se incluyen.
///
/// <para><b>Por qué no filtra por empresa</b></para>
/// No hace falta: <c>Comprobante</c> implementa <c>IEntidadDeEmpresa</c>, así que el filtro
/// global ya lo acota a la empresa activa. Repetir la condición aquí sería duplicar la regla
/// en un sitio donde nadie la mantendría (CLAUDE.md §5).
/// </summary>
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
}
