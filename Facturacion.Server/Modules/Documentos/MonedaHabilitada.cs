using Facturacion.Server.Data;
using Facturacion.Shared.Comun;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Documentos;

// Pesos o una moneda que la empresa agregó en Configuración; el selector ya las limita.
public static class MonedaHabilitada
{
    public static async Task<ErrorNegocio?> ExigirAsync(AppDbContext baseDeDatos, string moneda, CancellationToken ct)
    {
        if (moneda == "MXN") return null;

        // El filtro global deja ver solo las monedas de la empresa activa.
        var habilitada = await baseDeDatos.MonedasDeEmpresa.AsNoTracking()
            .AnyAsync(m => m.Moneda == moneda && m.Activa, ct);

        return habilitada
            ? null
            : ErrorNegocio.Validacion("moneda-no-habilitada",
                $"La empresa no usa la moneda {moneda}. Agrégala en Configuración, pestaña Monedas.");
    }
}
