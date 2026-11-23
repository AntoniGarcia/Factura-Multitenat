using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Facturacion.Server.Modules.Plataforma.Auth;

public sealed class ColaDeRecuperacionDeCliente(
    IServiceScopeFactory ambitos,
    IMemoryCache memoria,
    ILogger<ColaDeRecuperacionDeCliente> registro)
    : ColaDeCodigosDeRecuperacion(ambitos, memoria, registro, "cliente", "al sistema de facturación")
{
    internal static bool AdmiteRecuperacion(Usuario usuario)
        => usuario is { Activo: true, EmailConfirmed: true, CreadoPorAdministrador: false,
            Email: not null, PasswordHash: not null, SecurityStamp: not null };

    protected override async Task<Destinatario?> BuscarDestinatarioAsync(IServiceProvider servicios,
        string correoNormalizado, CancellationToken ct)
    {
        var usuario = await servicios.GetRequiredService<AppDbContext>().Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.NormalizedEmail == correoNormalizado, ct);
        return usuario is not null && AdmiteRecuperacion(usuario)
            ? new Destinatario(usuario.Id, usuario.Email!, usuario.SecurityStamp!)
            : null;
    }
}
