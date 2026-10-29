using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Documentos;
using Facturacion.Server.Data.Entidades.Transporte;
using Facturacion.Shared.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos;

/// <summary>
/// Si algún comprobante usa un registro de catálogo, en cualquier estatus: un borrador también
/// lo cuenta, porque borrar el registro lo dejaría apuntando a la nada. Clientes y productos
/// son de la cuenta (AGENTS.md §11), así que se buscan en los comprobantes de todas sus
/// empresas; las series siguen siendo de la empresa y les basta el filtro global.
/// </summary>
public sealed class UsoDeCatalogos(AppDbContext baseDeDatos) : IUsoDeCatalogos
{
    // El destinatario de un traslado Carta Porte no es el receptor del comprobante: se busca aparte.
    public async Task<bool> ClienteUsadoAsync(Guid clienteId, CancellationToken ct)
        => await ComprobantesDeLaCuenta().AnyAsync(c => c.ClienteId == clienteId, ct) ||
           await TrasladosDeLaCuenta().AnyAsync(t => t.ClienteDestinoId == clienteId, ct);

    public Task<bool> ProductoUsadoAsync(Guid productoId, CancellationToken ct)
        => ComprobantesDeLaCuenta().AnyAsync(c => c.Conceptos.Any(x => x.ProductoId == productoId), ct);

    // IgnoreQueryFilters justificado: el filtro global deja solo la empresa activa, y aquí
    // hacen falta todas las de la cuenta del claim, ninguna más.
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

    // Sin cuenta no hay a quién preguntar: responder «no se usa» permitiría borrar lo usado.
    private IQueryable<Guid> EmpresasDeLaCuenta()
    {
        var cuenta = baseDeDatos.CuentaActual
            ?? throw new InvalidOperationException("Consultar el uso de un catálogo exige una cuenta activa.");
        return baseDeDatos.Empresas.Where(e => e.CuentaId == cuenta).Select(e => e.Id);
    }

    public Task<bool> SerieUsadaAsync(Guid serieId, CancellationToken ct)
        => baseDeDatos.Comprobantes.AsNoTracking().AnyAsync(c => c.SerieId == serieId, ct);
}
