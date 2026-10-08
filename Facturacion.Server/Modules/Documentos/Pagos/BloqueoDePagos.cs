using Facturacion.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos.Pagos;

internal static class BloqueoDePagos
{
    // SQL Server coordina también instancias distintas. El bloqueo termina con la transacción,
    // antes de cualquier llamada al PAC; una empresa no bloquea a las demás.
    internal static async Task TomarAsync(AppDbContext db, Guid empresaId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("El bloqueo de pagos requiere una transacción.");
        var recurso = $"documentos-pagos:{empresaId:D}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @resultado int;
            EXEC @resultado = sys.sp_getapplock @Resource={recurso},
                @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
            IF @resultado < 0 THROW 51001, 'No se pudo obtener el bloqueo de pagos.', 1;
            """, ct);
    }
}
