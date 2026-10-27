using Facturacion.Server.Data;
using Facturacion.Shared.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos;

/// <summary>
/// Si algún comprobante de la empresa activa usa un registro de catálogo, en cualquier estatus:
/// un borrador también lo cuenta, porque borrar el registro lo dejaría apuntando a la nada.
/// EF aplica el filtro de empresa a los comprobantes; los conceptos se alcanzan a través de él.
/// </summary>
public sealed class UsoDeCatalogos(AppDbContext baseDeDatos) : IUsoDeCatalogos
{
    // El destinatario de un traslado Carta Porte no es el receptor del comprobante: se busca aparte.
    public async Task<bool> ClienteUsadoAsync(Guid clienteId, CancellationToken ct)
        => await baseDeDatos.Comprobantes.AsNoTracking().AnyAsync(c => c.ClienteId == clienteId, ct) ||
           await baseDeDatos.TrasladosCartaPorte.AsNoTracking().AnyAsync(t => t.ClienteDestinoId == clienteId, ct);

    public Task<bool> ProductoUsadoAsync(Guid productoId, CancellationToken ct)
        => baseDeDatos.Comprobantes.AsNoTracking()
            .AnyAsync(c => c.Conceptos.Any(x => x.ProductoId == productoId), ct);

    public Task<bool> SerieUsadaAsync(Guid serieId, CancellationToken ct)
        => baseDeDatos.Comprobantes.AsNoTracking().AnyAsync(c => c.SerieId == serieId, ct);
}
