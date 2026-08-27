using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Modules.Plataforma.Timbres;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
using Facturacion.Shared.Plataforma;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Operador.Compras;

/// <summary>
/// Las compras de timbres de todas las empresas, y las dos decisiones que el operador puede
/// tomar sobre una: acreditarla o descartarla.
///
/// <para><b>Acreditar crea dinero</b></para>
/// Mete timbres en la bolsa de un cliente sin que el sistema haya cobrado nada: el pago
/// ocurre fuera. Por eso el trabajo real no se rehace aquí, se delega en
/// <see cref="ServicioDeCompras.AcreditarAsync"/>, que ya lo hace con un procedimiento
/// almacenado transaccional e idempotente. Acreditar dos veces no entrega timbres dos veces.
///
/// <para><b>Aquí sí se esquiva el filtro de empresa</b></para>
/// <c>CompraTimbres</c> es de una empresa, y el operador tiene que ver las de todas. Es una de
/// las pocas excepciones legítimas al aislamiento, y por eso cada consulta la lleva anotada.
/// Lo que se devuelve son datos comerciales —qué paquete, cuánto, de qué empresa—, nunca datos
/// fiscales del inquilino.
/// </summary>
public sealed class ServicioDeComprasDeOperador(
    AppDbContext baseDeDatos,
    ServicioDeCompras compras,
    IServicioDeBitacora bitacora)
{
    /// <summary>
    /// Compras de todas las empresas, filtradas por estado y por texto de empresa o paquete.
    /// </summary>
    /// <param name="estado">Uno de <c>EstadosDeCompra</c>, o nulo para todas.</param>
    public async Task<PaginaDeComprasDeOperador> ListarAsync(
        string? estado, string? texto, int pagina, int tamano, CancellationToken ct)
    {
        // IgnoreQueryFilters justificado: el panel del proveedor consulta las compras de todos
        // sus clientes, que es precisamente lo que el filtro por empresa impide. Se compensa
        // con la política de operador en el endpoint: aquí no llega un inquilino.
        var consulta = baseDeDatos.ComprasTimbres
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado))
            consulta = consulta.Where(c => c.Estado == estado);

        // El join contra empresas y cuentas también lo lleva, por lo mismo.
        var empresas = baseDeDatos.Empresas.IgnoreQueryFilters().AsNoTracking();
        var cuentas = baseDeDatos.Cuentas.AsNoTracking();

        var unidas =
            from compra in consulta
            join empresa in empresas on compra.EmpresaId equals empresa.Id
            join cuenta in cuentas on empresa.CuentaId equals cuenta.Id
            select new { compra, empresa, cuenta };

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var patron = $"%{texto.Trim()}%";

            unidas = unidas.Where(u =>
                EF.Functions.Like(u.empresa.NombreFiscal, patron) ||
                EF.Functions.Like(u.empresa.Rfc, patron) ||
                EF.Functions.Like(u.compra.NombrePaquete, patron));
        }

        var total = await unidas.CountAsync(ct);

        // Las más recientes primero: al acreditar se trabaja sobre lo que acaba de entrar.
        var elementos = await unidas
            .OrderByDescending(u => u.compra.CreadaUtc)
            .Skip(pagina * tamano)
            .Take(tamano)
            .Select(u => new CompraDeOperadorDto(
                u.compra.Id,
                u.empresa.NombreFiscal,
                u.empresa.Rfc,
                u.cuenta.Nombre,
                u.compra.NombrePaquete,
                u.compra.CantidadTimbres,
                u.compra.PrecioPorTimbre,
                u.compra.PrecioTotal,
                u.compra.Estado,
                u.compra.CreadaUtc,
                u.compra.AcreditadaUtc,
                u.compra.VenceUtc,
                u.compra.CanceladaUtc,
                u.compra.MotivoCancelacion))
            .ToListAsync(ct);

        return new PaginaDeComprasDeOperador(elementos, total);
    }

    /// <summary>
    /// Acredita el pago: los timbres entran a la bolsa de la empresa que compró.
    /// <para>
    /// Recibe el identificador de la <b>compra</b>, nunca el de una empresa. La empresa se
    /// deduce en el servidor a partir de la compra, así que la regla de ARQUITECTURA.md §4
    /// —el cliente no manda identificadores de empresa— sigue intacta.
    /// </para>
    /// </summary>
    public async Task<Resultado<CompraDto>> AcreditarAsync(Guid compraId, CancellationToken ct)
        => await compras.AcreditarAsync(compraId, ct);

    /// <summary>
    /// Descarta una compra que no se va a cobrar. No toca la bolsa: una compra pendiente nunca
    /// entregó timbres, así que descartarla no quita nada.
    /// <para>
    /// El cambio de estado va condicionado a que siga pendiente, en una sola sentencia. Si
    /// alguien la acredita en el mismo instante, gana quien llegue primero y el otro recibe un
    /// conflicto en vez de dejar la compra en un estado imposible.
    /// </para>
    /// </summary>
    public async Task<Resultado<bool>> RechazarAsync(Guid compraId, string motivo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            return ErrorNegocio.Validacion("motivo-requerido", "Escribe por qué se descarta la compra.");

        var recortado = motivo.Trim();
        if (recortado.Length > 300) recortado = recortado[..300];

        var ahora = DateTime.UtcNow;

        // IgnoreQueryFilters justificado: mismo motivo que el listado. La condición por estado
        // es la que hace segura la operación frente a una acreditación simultánea.
        var filas = await baseDeDatos.ComprasTimbres
            .IgnoreQueryFilters()
            .Where(c => c.Id == compraId && c.Estado == EstadosDeCompra.PendienteDePago)
            .ExecuteUpdateAsync(c => c
                .SetProperty(x => x.Estado, EstadosDeCompra.Cancelada)
                .SetProperty(x => x.CanceladaUtc, ahora)
                .SetProperty(x => x.MotivoCancelacion, recortado), ct);

        if (filas == 0)
            return ErrorNegocio.Conflicto(
                "compra-no-rechazable",
                "Esa compra no existe o ya no está pendiente de pago.");

        // IgnoreQueryFilters justificado: se relee para saber a qué empresa atribuir el
        // registro de bitácora, y sigue sin haber empresa activa en esta petición.
        var compra = await baseDeDatos.ComprasTimbres
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(c => c.Id == compraId, ct);

        bitacora.Registrar(
            EntidadesDeBitacora.CompraTimbres,
            compraId.ToString(),
            AccionesDeBitacora.CompraRechazada,
            despues: new { compra.NombrePaquete, compra.PrecioTotal, Motivo = recortado },
            empresaId: compra.EmpresaId);

        await baseDeDatos.SaveChangesAsync(ct);

        return true;
    }
}
