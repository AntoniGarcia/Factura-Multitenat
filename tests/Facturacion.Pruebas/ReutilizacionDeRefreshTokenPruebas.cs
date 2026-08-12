using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Plataforma.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Facturacion.Pruebas;

/// <summary>
/// La prueba obligatoria de la fase 1, y no es negociable.
///
/// <para>
/// Un refresh token consumido que vuelve a llegar significa que dos partes tienen la misma
/// cookie: la legítima y quien la copió. Como no hay forma de distinguirlas, se invalida la
/// familia completa y las dos quedan fuera.
/// </para>
/// <para>
/// Se prueba contra SQL Server porque lo que hay que comprobar es que la invalidación
/// <b>quedó persistida</b>: si solo ocurriera en memoria, el ladrón seguiría dentro.
/// </para>
/// </summary>
public sealed class ReutilizacionDeRefreshTokenPruebas : IAsyncLifetime
{
    private const string Conexion =
        "Server=.;Database=FacturacionPruebasAuth;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    private readonly Guid _cuenta = Guid.NewGuid();
    private readonly Guid _usuario = Guid.NewGuid();

    private static AppDbContext Contexto()
    {
        var tenencia = new ContextoEmpresaFijo();
        var opciones = new DbContextOptionsBuilder<AppDbContext>().Configurar(Conexion, tenencia).Options;

        return new AppDbContext(opciones, tenencia);
    }

    private static ServicioDeRefreshTokens Servicio(AppDbContext baseDeDatos)
    {
        var bitacora = new ServicioDeBitacora(
            baseDeDatos, new ContextoEmpresaFijo(), new HttpContextAccessor());

        return new ServicioDeRefreshTokens(
            baseDeDatos, bitacora, NullLogger<ServicioDeRefreshTokens>.Instance);
    }

    public async Task InitializeAsync()
    {
        await using var baseDeDatos = Contexto();
        await baseDeDatos.Database.EnsureDeletedAsync();
        await baseDeDatos.Database.MigrateAsync();

        baseDeDatos.Cuentas.Add(new Cuenta
        {
            Id = _cuenta,
            Nombre = "Cuenta de prueba",
            CorreoContacto = "pruebas@ejemplo.mx",
            FechaAltaUtc = DateTime.UtcNow
        });

        baseDeDatos.Users.Add(new Usuario
        {
            Id = _usuario,
            Nombre = "Usuario de prueba",
            CuentaId = _cuenta,
            UserName = "pruebas@ejemplo.mx",
            NormalizedUserName = "PRUEBAS@EJEMPLO.MX",
            Email = "pruebas@ejemplo.mx",
            NormalizedEmail = "PRUEBAS@EJEMPLO.MX",
            SecurityStamp = Guid.NewGuid().ToString(),
            FechaAltaUtc = DateTime.UtcNow
        });

        await baseDeDatos.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var baseDeDatos = Contexto();
        await baseDeDatos.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Reutilizar_un_token_consumido_invalida_la_familia_completa()
    {
        Guid familiaId;
        string primero, segundo;

        // Sesión normal: se abre la familia y se canjea una vez, como haría el Client a los
        // quince minutos.
        await using (var baseDeDatos = Contexto())
        {
            var servicio = Servicio(baseDeDatos);

            var inicial = servicio.CrearFamilia(_usuario, TimeSpan.FromHours(12), "127.0.0.1", "pruebas");
            familiaId = inicial.Registro.FamiliaId;
            primero = inicial.EnClaro;
            await baseDeDatos.SaveChangesAsync();

            var rotado = await servicio.RotarAsync(primero, "127.0.0.1", "pruebas", CancellationToken.None);

            Assert.True(rotado.EsExito);
            segundo = rotado.Valor.EnClaro;
        }

        // Ahora llega otra vez el token que ya se canjeó: es la señal de que alguien copió
        // la cookie.
        await using (var baseDeDatos = Contexto())
        {
            var reutilizado = await Servicio(baseDeDatos)
                .RotarAsync(primero, "10.0.0.9", "ladron", CancellationToken.None);

            Assert.True(reutilizado.EsFallo);
        }

        await using (var baseDeDatos = Contexto())
        {
            var familia = await baseDeDatos.RefreshTokens
                .Where(t => t.FamiliaId == familiaId)
                .ToListAsync();

            Assert.Equal(2, familia.Count);
            Assert.All(familia, token => Assert.NotNull(token.RevocadoUtc));

            var incidente = await baseDeDatos.Bitacora.IgnoreQueryFilters()
                .AnyAsync(b => b.Accion == AccionesDeBitacora.ReutilizacionDeToken);

            Assert.True(incidente, "La reutilización tiene que quedar registrada en la bitácora.");
        }

        // Y el token que hasta hace un momento era válido tampoco sirve ya: esa es la parte
        // que importa. Sin esto, quien robó la cookie seguiría dentro.
        await using (var baseDeDatos = Contexto())
        {
            var vivo = await Servicio(baseDeDatos)
                .RotarAsync(segundo, "127.0.0.1", "pruebas", CancellationToken.None);

            Assert.True(vivo.EsFallo);
        }
    }
}
