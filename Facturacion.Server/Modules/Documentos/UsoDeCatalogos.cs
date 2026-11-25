using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Data.Entidades.Transporte;
using Facturacion.Shared.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos;

/// Si algún comprobante usa un registro, en cualquier estatus (un borrador también cuenta).
/// Clientes y productos son de la cuenta y se buscan en todas sus empresas;
/// las series son de la empresa y les basta el filtro global.
public sealed class UsoDeCatalogos(AppDbContext baseDeDatos) : IUsoDeCatalogos
{
    // El destinatario de un traslado no es el receptor del comprobante: se busca aparte.
    public async Task<bool> ClienteUsadoAsync(Guid clienteId, CancellationToken ct)
        => await ComprobantesDeLaCuenta().AnyAsync(c => c.ClienteId == clienteId, ct) ||
           await TrasladosDeLaCuenta().AnyAsync(t => t.ClienteDestinoId == clienteId, ct);

    public async Task<bool> ProductoUsadoAsync(Guid productoId, CancellationToken ct)
        => await ComprobantesDeLaCuenta().AnyAsync(c => c.Conceptos.Any(x => x.ProductoId == productoId), ct) ||
           await TrasladosDeLaCuenta().AnyAsync(t => t.Mercancias.Any(x => x.ProductoId == productoId), ct);

    // IgnoreQueryFilters: hacen falta todas las empresas de la cuenta, no solo la activa.
    private IQueryable<Comprobante> ComprobantesDeLaCuenta()
    {
        var empresas = EmpresasDeLaCuenta();
        return baseDeDatos.Comprobantes.IgnoreQueryFilters().AsNoTracking().Where(c => empresas.Contains(c.EmpresaId));
    }

    private IQueryable<TrasladoCartaPorte> TrasladosDeLaCuenta()
    {
        var empresas = EmpresasDeLaCuenta();
        return baseDeDatos.TrasladosCartaPorte.IgnoreQueryFilters().AsNoTracking().Where(t => empresas.Contains(t.EmpresaId));
    }

    // Sin cuenta, responder «no se usa» permitiría borrar lo usado.
    private IQueryable<Guid> EmpresasDeLaCuenta()
    {
        var cuenta = baseDeDatos.CuentaActual
            ?? throw new InvalidOperationException("Consultar el uso de un catálogo exige una cuenta activa.");
        return baseDeDatos.Empresas.Where(e => e.CuentaId == cuenta).Select(e => e.Id);
    }

    public Task<bool> SerieUsadaAsync(Guid serieId, CancellationToken ct)
        => baseDeDatos.Comprobantes.AsNoTracking().AnyAsync(c => c.SerieId == serieId, ct);
}
