using System.Data.Common;
using System.Security.Claims;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Seguridad;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Server.Modules.Plataforma;
using Facturacion.Server.Modules.Plataforma.Auth;
using Facturacion.Shared.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Facturacion.Pruebas;

public sealed class CierreDeSesionesPruebas
{
    [Fact]
    public async Task Rotacion_concurrente_y_cambio_de_empresa_rechazan_familias_invalidas()
    {
        var nombre = "FacturacionCierreSesion_" + Guid.NewGuid().ToString("N");
        var conexion = $"Server=.;Database={nombre};Integrated Security=True;TrustServerCertificate=True;Encrypt=False";
        var cuenta = Guid.NewGuid(); var usuario = Guid.NewGuid(); var operador = Guid.NewGuid(); var empresa = Guid.NewGuid();
        AppDbContext Db(DbCommandInterceptor? interceptor = null)
        {
            var opciones = new DbContextOptionsBuilder<AppDbContext>().Configurar(conexion, ContextoEmpresaFijo.SinEmpresa);
            if (interceptor is not null) opciones.AddInterceptors(interceptor);
            return new(opciones.Options, ContextoEmpresaFijo.SinEmpresa);
        }
        IServicioDeBitacora Bitacora(AppDbContext db) => new ServicioDeBitacora(db, ContextoEmpresaFijo.SinEmpresa,
            ContextoDeOperadorFijo.SinOperador, new HttpContextAccessor());
        ServicioDeRefreshTokens Cliente(AppDbContext db) => new(db, Bitacora(db), NullLogger<ServicioDeRefreshTokens>.Instance);
        ServicioDeRefreshTokensDeOperador Operador(AppDbContext db) => new(db, Bitacora(db), NullLogger<ServicioDeRefreshTokensDeOperador>.Instance);
        try
        {
            await using (var db = Db())
            {
                await db.Database.MigrateAsync();
                db.Cuentas.Add(new() { Id = cuenta, Nombre = "Prueba", CorreoContacto = "prueba@example.invalid" });
                db.Users.Add(new() { Id = usuario, CuentaId = cuenta, Nombre = "Prueba", UserName = "prueba@example.invalid", Activo = true });
                db.OperadoresPlataforma.Add(new() { Id = operador, Nombre = "Prueba", Correo = "op@example.invalid", CorreoNormalizado = "OP@EXAMPLE.INVALID", HashContrasena = "sin-inicio-de-sesion" });
                db.Empresas.Add(new() { Id = empresa, CuentaId = cuenta, Rfc = "EKU9003173C9", NombreFiscal = "ESCUELA KEMPER URGATE", RegimenFiscal = "601", CodigoPostalExpedicion = "42000", ZonaHoraria = "America/Mexico_City" });
                db.UsuariosEmpresas.Add(new() { UsuarioId = usuario, EmpresaId = empresa, Activo = true });
                await db.SaveChangesAsync();
            }
            foreach (var esOperador in new[] { false, true })
            {
                string token; Guid familia;
                await using (var db = Db())
                {
                    if (esOperador) { var creado = Operador(db).CrearFamilia(operador, TimeSpan.FromHours(1), null, null); token = creado.EnClaro; familia = creado.Registro.FamiliaId; }
                    else { var creado = Cliente(db).CrearFamilia(usuario, TimeSpan.FromHours(1), null, null); token = creado.EnClaro; familia = creado.Registro.FamiliaId; }
                    await db.SaveChangesAsync();
                }
                var barrera = new LecturasSimultaneas();
                async Task<bool> Rotar()
                {
                    await using var db = Db(barrera);
                    return esOperador ? (await Operador(db).RotarAsync(token, null, null, default)).EsExito
                        : (await Cliente(db).RotarAsync(token, null, null, default)).EsExito;
                }
                var resultados = await Task.WhenAll(Rotar(), Rotar());
                Assert.Single(resultados, r => r);
                await using var lectura = Db();
                if (esOperador) Assert.False(await lectura.RefreshTokensOperador.AnyAsync(t => t.FamiliaId == familia && t.RevocadoUtc == null));
                else Assert.False(await lectura.RefreshTokens.AnyAsync(t => t.FamiliaId == familia && t.RevocadoUtc == null));
            }
            var servicios = new ServiceCollection(); servicios.AddLogging(); servicios.AddMemoryCache(); servicios.AddHttpContextAccessor();
            servicios.AddSingleton<IControlDeIntentos, ControlDeIntentos>();
            servicios.AddSingleton<IContextoEmpresaInterno>(ContextoEmpresaFijo.SinEmpresa);
            servicios.AddSingleton<IContextoDeOperador>(ContextoDeOperadorFijo.SinOperador);
            servicios.AddDbContext<AppDbContext>(o => o.Configurar(conexion, ContextoEmpresaFijo.SinEmpresa));
            servicios.AddScoped<IServicioDeBitacora, ServicioDeBitacora>();
            servicios.AddPlataforma(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["Jwt:ClaveDeFirma"] = new string('t', 64), ["Jwt:Emisor"] = "prueba", ["Jwt:Audiencia"] = "prueba" }).Build());
            await using var proveedor = servicios.BuildServiceProvider();
            async Task<bool> Cambiar(Guid familia)
            {
                using var scope = proveedor.CreateScope();
                scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
                { User = new ClaimsPrincipal(new ClaimsIdentity([new(ClavesDeClaim.Usuario, usuario.ToString()), new(ClavesDeClaim.Familia, familia.ToString())], "prueba")) };
                return (await scope.ServiceProvider.GetRequiredService<ServicioDeAutenticacion>().CambiarEmpresaAsync(empresa, default)).EsExito;
            }
            Guid vigente;
            await using (var db = Db()) { vigente = Cliente(db).CrearFamilia(usuario, TimeSpan.FromHours(1), null, null).Registro.FamiliaId; await db.SaveChangesAsync(); }
            Assert.True(await Cambiar(vigente)); Assert.False(await Cambiar(Guid.NewGuid()));
            await using (var db = Db()) { await Cliente(db).InvalidarFamiliaAsync(vigente, "prueba", default); await db.SaveChangesAsync(); }
            Assert.False(await Cambiar(vigente));
            await using (var db = Db())
            {
                vigente = Cliente(db).CrearFamilia(usuario, TimeSpan.FromHours(1), null, null).Registro.FamiliaId;
                (await db.Users.SingleAsync(u => u.Id == usuario)).Activo = false; await db.SaveChangesAsync();
            }
            Assert.False(await Cambiar(vigente));
        }
        finally { await using var db = Db(); Assert.Equal(nombre, db.Database.GetDbConnection().Database); SqlConnection.ClearAllPools(); await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class LecturasSimultaneas : DbCommandInterceptor
    {
        private int _lecturas;
        private readonly TaskCompletionSource _listas = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("[HashToken] =", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref _lecturas) == 2) _listas.TrySetResult();
                await _listas.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }
}
