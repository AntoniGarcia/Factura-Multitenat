using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Shared.Operador;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Operador.Tablero;

/// <summary>
/// Las cifras del negocio del SaaS, en una sola petición.
///
/// <para><b>Por qué compuesto en el servidor</b></para>
/// Son siete consultas. Pedirlas por separado desde el navegador serían siete viajes de red
/// para pintar una pantalla, y en WebAssembly eso se nota. Mismo criterio que el tablero del
/// inquilino.
///
/// <para>
/// Las consultas sobre compras y bolsas llevan <c>IgnoreQueryFilters</c> anotado: son de
/// empresa y el proveedor las mira de todas. No se proyecta ningún dato fiscal.
/// </para>
/// </summary>
public sealed class ServicioDeTableroDeOperador(AppDbContext baseDeDatos)
{
    private const int DiasDeAvisoDeVencimiento = 30;

    public async Task<TableroDeOperadorDto> ObtenerAsync(CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        var inicioDelMes = new DateTime(ahora.Year, ahora.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var limiteDeAviso = ahora.AddDays(DiasDeAvisoDeVencimiento);

        // IgnoreQueryFilters justificado: el panel resume las compras de todas las empresas.
        var compras = baseDeDatos.ComprasTimbres.IgnoreQueryFilters().AsNoTracking();

        var pendientes = compras.Where(c => c.Estado == EstadosDeCompra.PendienteDePago);

        var comprasPendientes = await pendientes.CountAsync(ct);
        var montoPendiente = await pendientes.SumAsync(c => (decimal?)c.PrecioTotal, ct) ?? 0m;

        // El ingreso se cuenta por la fecha de acreditación y no por la de solicitud: el mes
        // en que entró el dinero es aquel en que se cobró, no aquel en que se pidió.
        var delMes = compras.Where(c =>
            c.Estado == EstadosDeCompra.Pagada &&
            c.AcreditadaUtc != null &&
            c.AcreditadaUtc >= inicioDelMes);

        var ingresoDelMes = await delMes.SumAsync(c => (decimal?)c.PrecioTotal, ct) ?? 0m;
        var timbresDelMes = await delMes.SumAsync(c => (int?)c.CantidadTimbres, ct) ?? 0;

        var cuentasActivas = await baseDeDatos.Cuentas.CountAsync(c => c.Activa, ct);
        var empresasActivas = await baseDeDatos.Empresas.CountAsync(e => e.Activa, ct);

        var membresiasPorVencer = await baseDeDatos.Membresias
            .CountAsync(m => m.Estado == EstadosDeMembresia.Activa
                             && m.FinUtc >= ahora
                             && m.FinUtc <= limiteDeAviso, ct);

        var ultimas = await (
            from compra in compras
            join empresa in baseDeDatos.Empresas.IgnoreQueryFilters().AsNoTracking()
                on compra.EmpresaId equals empresa.Id
            join cuenta in baseDeDatos.Cuentas.AsNoTracking()
                on empresa.CuentaId equals cuenta.Id
            where compra.Estado == EstadosDeCompra.Pagada
            orderby compra.AcreditadaUtc descending
            select new CompraDeOperadorDto(
                compra.Id, empresa.NombreFiscal, empresa.Rfc, cuenta.Nombre,
                compra.NombrePaquete, compra.CantidadTimbres, compra.PrecioPorTimbre,
                compra.PrecioTotal, compra.Estado, compra.CreadaUtc, compra.AcreditadaUtc,
                compra.VenceUtc, compra.CanceladaUtc, compra.MotivoCancelacion))
            .Take(5)
            .ToListAsync(ct);

        var porMes = await SerieMensualAsync(compras, inicioDelMes, ct);
        var porPaquete = await VentasPorPaqueteAsync(compras, ct);

        return new TableroDeOperadorDto(
            comprasPendientes, montoPendiente, ingresoDelMes, timbresDelMes,
            cuentasActivas, empresasActivas, membresiasPorVencer, ultimas,
            porMes, porPaquete);
    }

    /// <summary>
    /// Ingreso y timbres de los últimos seis meses, incluido el actual. Los meses sin ventas
    /// salen en cero en vez de faltar: una gráfica que se salta los meses vacíos miente sobre
    /// la forma de la curva.
    /// </summary>
    private static async Task<IReadOnlyList<PuntoMensualDto>> SerieMensualAsync(
        IQueryable<CompraTimbres> compras, DateTime inicioDelMes, CancellationToken ct)
    {
        const int meses = 6;

        var desde = inicioDelMes.AddMonths(-(meses - 1));

        var agrupado = await compras
            .Where(c => c.Estado == EstadosDeCompra.Pagada
                        && c.AcreditadaUtc != null
                        && c.AcreditadaUtc >= desde)
            .GroupBy(c => new { c.AcreditadaUtc!.Value.Year, c.AcreditadaUtc!.Value.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Ingreso = g.Sum(c => c.PrecioTotal),
                Timbres = g.Sum(c => c.CantidadTimbres)
            })
            .ToListAsync(ct);

        var nombres = new[] { "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic" };

        return Enumerable.Range(0, meses)
            .Select(i =>
            {
                var mes = desde.AddMonths(i);
                var dato = agrupado.FirstOrDefault(a => a.Year == mes.Year && a.Month == mes.Month);

                return new PuntoMensualDto(
                    $"{nombres[mes.Month - 1]} {mes:yy}",
                    dato?.Ingreso ?? 0m,
                    dato?.Timbres ?? 0);
            })
            .ToList();
    }

    /// <summary>Los cinco paquetes que más ingreso han dejado, de todos los tiempos.</summary>
    private static async Task<IReadOnlyList<VentaPorPaqueteDto>> VentasPorPaqueteAsync(
        IQueryable<CompraTimbres> compras, CancellationToken ct)
    {
        // Se proyecta a un tipo anónimo y no directo al DTO: EF no sabe traducir un record
        // posicional dentro de un GroupBy, y falla al compilar la consulta en vez de al
        // escribirla. El mapeo final se hace ya en memoria, sobre cinco renglones.
        var agrupado = await compras
            .Where(c => c.Estado == EstadosDeCompra.Pagada)
            // Se agrupa por el nombre copiado en la compra y no por PaqueteId: así un paquete
            // retirado del catálogo sigue apareciendo con lo que vendió mientras existió.
            .GroupBy(c => c.NombrePaquete)
            .Select(g => new
            {
                Nombre = g.Key,
                Compras = g.Count(),
                Ingreso = g.Sum(c => c.PrecioTotal)
            })
            .OrderByDescending(v => v.Ingreso)
            .Take(5)
            .ToListAsync(ct);

        return agrupado
            .Select(a => new VentaPorPaqueteDto(a.Nombre, a.Compras, a.Ingreso))
            .ToList();
    }
}
