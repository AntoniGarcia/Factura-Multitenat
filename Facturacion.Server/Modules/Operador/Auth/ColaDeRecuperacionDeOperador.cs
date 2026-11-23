using Facturacion.Server.Data;
using Facturacion.Server.Infra.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Facturacion.Server.Modules.Operador.Auth;

public sealed class ColaDeRecuperacionDeOperador(
    IServiceScopeFactory ambitos,
    IMemoryCache memoria,
    ILogger<ColaDeRecuperacionDeOperador> registro)
    : ColaDeCodigosDeRecuperacion(ambitos, memoria, registro, "operador", "al panel de operador")
{
    protected override Task<Destinatario?> BuscarDestinatarioAsync(IServiceProvider servicios,
        string correoNormalizado, CancellationToken ct)
        => servicios.GetRequiredService<AppDbContext>().OperadoresPlataforma.AsNoTracking()
            .Where(o => o.Activo && o.CorreoNormalizado == correoNormalizado)
            .Select(o => new Destinatario(o.Id, o.Correo, o.HashContrasena))
            .SingleOrDefaultAsync(ct);
}
