using System.Text;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Idempotencia;
using Facturacion.Server.Modules.Plataforma.Auth;
using Facturacion.Server.Modules.Plataforma.Catalogos;
using Facturacion.Server.Modules.Plataforma.Clientes;
using Facturacion.Server.Modules.Plataforma.Empresas;
using Facturacion.Server.Modules.Plataforma.Folios;
using Facturacion.Server.Modules.Plataforma.Productos;
using Facturacion.Server.Modules.Plataforma.Tablero;
using Facturacion.Server.Modules.Plataforma.Timbres;
using Facturacion.Server.Modules.Plataforma.Usuarios;
using Facturacion.Shared.Contratos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

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
        var jwt = LeerOpcionesDeJwt(configuracion);

        servicios.AddOptions<OpcionesDeJwt>()
            .Bind(configuracion.GetSection(OpcionesDeJwt.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

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

                // El plazo fijo lo pone Identity; el castigo creciente lo aplica
                // ServicioDeAutenticacion encima de este bloqueo.
                opciones.Lockout.AllowedForNewUsers = true;
                opciones.Lockout.MaxFailedAccessAttempts = 5;
                opciones.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(1);
            })
            .AddEntityFrameworkStores<AppDbContext>();

        servicios.AddScoped<ServicioDeTokens>();
        servicios.AddScoped<ServicioDeRefreshTokens>();
        servicios.AddScoped<ServicioDeAutenticacion>();

        servicios.AddOptions<OpcionesDeSoporte>().Bind(configuracion.GetSection(OpcionesDeSoporte.Seccion));
        servicios.AddScoped<ServicioDeInvitaciones>();

        servicios.AddScoped<IServicioCatalogosSat, ServicioCatalogosSat>();

        servicios.AddScoped<ServicioDeEmpresa>();
        servicios.AddScoped<ServicioDeLogo>();
        servicios.AddScoped<ServicioDeCsd>();
        servicios.AddScoped<ServicioDeSeries>();

        servicios.AddScoped<ValidadorDeProducto>();
        servicios.AddScoped<ServicioDeProductos>();

        servicios.AddScoped<ValidadorDeCliente>();
        servicios.AddScoped<ServicioDeClientes>();

        servicios.AddScoped<ServicioDeCompras>();
        servicios.AddScoped<FiltroDeIdempotencia>();

        servicios.AddScoped<ServicioDeTablero>();

        // Devuelve los timbres de los timbrados que murieron a la mitad. Sin él, cada
        // reserva sin resolver congela un timbre pagado para siempre.
        servicios.AddHostedService<BarridoDeReservasDeTimbre>();

        // Las implementaciones que la mitad B consume por contrato (REPARTO-EQUIPO.md §5).
        // Son seis; la séptima, IResumenDocumentos, va en sentido contrario: la escribe la
        // mitad B y la consume el tablero. VerificacionDeContratos comprueba al arrancar que
        // esta lista siga completa.
        servicios.AddScoped<IServicioEmpresaEmisora, ServicioEmpresaEmisora>();
        servicios.AddScoped<IProveedorCsdParaTimbrado, ProveedorCsdParaTimbrado>();
        servicios.AddScoped<IServicioFolios, ServicioDeFolios>();
        servicios.AddScoped<IServicioClientes>(sp => sp.GetRequiredService<ServicioDeClientes>());
        servicios.AddScoped<IServicioProductos>(sp => sp.GetRequiredService<ServicioDeProductos>());
        servicios.AddScoped<IServicioTimbres, ServicioDeTimbres>();

        servicios
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opciones =>
            {
                // Los claims viajan con los nombres cortos que emitimos; sin esto el
                // middleware los traduciría a las URI largas de WS-Federation.
                opciones.MapInboundClaims = false;

                opciones.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Emisor,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audiencia,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.ClaveDeFirma)),
                    ValidateLifetime = true,

                    // Sin tolerancia: quince minutos son quince minutos. La tolerancia por
                    // omisión de cinco minutos alargaría cada token a veinte.
                    ClockSkew = TimeSpan.Zero
                };

                // Que el 401 y el 403 salgan también en Problem Details con traza, como el
                // resto de los errores del sistema.
                opciones.Events = new JwtBearerEvents
                {
                    OnChallenge = async contexto =>
                    {
                        contexto.HandleResponse();
                        await ResultadosDeError.EscribirProblema(
                            contexto.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            "no-autenticado",
                            "Necesitas iniciar sesión",
                            "El token de acceso falta o ya expiró.");
                    },
                    OnForbidden = contexto => ResultadosDeError.EscribirProblema(
                        contexto.HttpContext,
                        StatusCodes.Status403Forbidden,
                        "sin-permiso",
                        "No tienes permiso para esta operación",
                        "Tu usuario no tiene el permiso que exige esta operación en la empresa activa.")
                };
            });

        servicios.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AgregarPoliticasDePermiso();

        return servicios;
    }

    public static WebApplication MapPlataforma(this WebApplication aplicacion)
    {
        aplicacion.MapAuth();
        aplicacion.MapPerfil();
        aplicacion.MapCatalogos();
        aplicacion.MapEmpresas();
        aplicacion.MapFolios();
        aplicacion.MapClientes();
        aplicacion.MapProductos();
        aplicacion.MapTimbres();
        aplicacion.MapUsuarios();
        aplicacion.MapTablero();

        return aplicacion;
    }

    private static OpcionesDeJwt LeerOpcionesDeJwt(IConfiguration configuracion)
    {
        var jwt = configuracion.GetSection(OpcionesDeJwt.Seccion).Get<OpcionesDeJwt>() ?? new OpcionesDeJwt();

        // Se falla al arrancar y no en la primera petición: una clave de firma ausente no es
        // algo que se deba descubrir cuando alguien intenta iniciar sesión.
        if (jwt.ClaveDeFirma.Length < 32)
            throw new InvalidOperationException(
                "Falta 'Jwt:ClaveDeFirma' o tiene menos de 32 caracteres. Se configura en " +
                "appsettings.Development.json o en una variable de entorno; nunca en el repositorio.");

        return jwt;
    }
}
