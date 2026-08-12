using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Microsoft.AspNetCore.Identity;

namespace Facturacion.Server.Modules.Plataforma;

/// <summary>
/// Punto de entrada de la mitad A. Todo registro de servicios y todo endpoint de plataforma
/// se declara aquí dentro, nunca en <c>Program.cs</c> (REPARTO-EQUIPO.md §5).
/// </summary>
public static class PlataformaModule
{
    public static IServiceCollection AddPlataforma(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        // AddIdentityCore y no AddIdentity: no queremos SignInManager, que arrastra esquemas
        // de autenticación por cookie. Identity se usa solo como almacén de usuarios, motor
        // de hash y bloqueo por intentos; la emisión de tokens es propia (CLAUDE.md §4).
        servicios
            .AddIdentityCore<Usuario>(opciones =>
            {
                opciones.User.RequireUniqueEmail = true;

                opciones.Password.RequiredLength = 12;
                opciones.Password.RequireDigit = true;
                opciones.Password.RequireLowercase = true;
                opciones.Password.RequireUppercase = true;
                opciones.Password.RequireNonAlphanumeric = false;

                // Bloqueo por intentos fallidos. El límite por IP y el bloqueo creciente
                // son de la fase 1; esto es lo que aporta el almacén.
                opciones.Lockout.AllowedForNewUsers = true;
                opciones.Lockout.MaxFailedAccessAttempts = 5;
                opciones.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<AppDbContext>();

        return servicios;
    }

    public static WebApplication MapPlataforma(this WebApplication aplicacion)
    {
        // Los endpoints de autenticación son la fase 1. Todos cuelgan de /api/ para que la
        // exclusión del service worker y el MapFallbackToFile no se pisen.
        return aplicacion;
    }
}
