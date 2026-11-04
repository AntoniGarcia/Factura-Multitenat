using Facturacion.Server.Data;
using Facturacion.Server.Infra.Tenencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Pruebas;

public sealed class ReservaDeFoliosProveedorPruebas
{
    [Fact]
    public async Task Solicitudes_concurrentes_no_repiten_folio_y_separan_rfc_y_serie()
    {
        var nombre = "FacturacionPruebaFolioProveedor_" + Guid.NewGuid().ToString("N");
        var cadena = new SqlConnectionStringBuilder
        { DataSource = ".", InitialCatalog = nombre, IntegratedSecurity = true, Encrypt = false }.ConnectionString;
        AppDbContext Contexto()
        {
            var tenencia = ContextoEmpresaFijo.SinEmpresa;
            return new(new DbContextOptionsBuilder<AppDbContext>().Configurar(cadena, tenencia).Options, tenencia);
        }
        async Task<int> Reservar(string rfc, string serie)
        {
            await using var db = Contexto();
            return (await db.Database.SqlQueryRaw<int>("EXEC dbo.ReservarFolioProveedor @Rfc, @Serie",
                new SqlParameter("@Rfc", rfc), new SqlParameter("@Serie", serie)).ToListAsync()).Single();
        }
        try
        {
            await using (var db = Contexto())
            {
                await db.Database.MigrateAsync();
                await db.Database.ExecuteSqlRawAsync("ALTER DATABASE CURRENT SET COMPATIBILITY_LEVEL = 120;");
            }
            var inicio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tareas = Enumerable.Range(0, 25).Select(async _ =>
            { await inicio.Task; return await Reservar("EKU9003173C9", "PAQ"); }).ToArray();
            inicio.SetResult();
            var folios = await Task.WhenAll(tareas);
            Assert.Equal(Enumerable.Range(1, 25), folios.Order());
            Assert.Equal(26, await Reservar("EKU9003173C9", "PAQ"));
            Assert.Equal(1, await Reservar("EKU9003173C9", "OTRA"));
            Assert.Equal(1, await Reservar("AAA010101AAA", "PAQ"));
        }
        finally
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(nombre, "^FacturacionPruebaFolioProveedor_[0-9a-f]{32}$"))
                throw new InvalidOperationException("Nombre inesperado de base de pruebas.");
            await using var db = Contexto();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
