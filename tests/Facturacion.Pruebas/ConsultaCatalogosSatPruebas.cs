using System.Security.Claims;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Operador.Auth;
using Facturacion.Server.Modules.Operador.Catalogos;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Operador;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Facturacion.Pruebas;

public sealed class ConsultaCatalogosSatPruebas : IAsyncLifetime
{
    private readonly string _baseTemporal = "FacturacionPruebasCatalogos_" + Guid.NewGuid().ToString("N");

    private AppDbContext Abrir()
    {
        var contexto = ContextoEmpresaFijo.SinEmpresa;
        return new(new DbContextOptionsBuilder<AppDbContext>().Configurar(
            $"Server=.;Database={_baseTemporal};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False", contexto).Options, contexto);
    }

    public async Task InitializeAsync()
    {
        await using var db = Abrir();
        await db.Database.EnsureCreatedAsync();
        db.SatMonedas.AddRange(
            new SatMoneda { Clave = "MXN", Descripcion = "Peso mexicano", Decimales = 2, Vigente = true, FechaInicioVigencia = new(2020, 1, 1) },
            new SatMoneda { Clave = "USD", Descripcion = "Dólar", Decimales = 2, Vigente = true, FechaInicioVigencia = new(2020, 1, 1) },
            new SatMoneda { Clave = "ZZZ", Descripcion = "Prueba no vigente", Decimales = 2, Vigente = false, FechaInicioVigencia = new(2020, 1, 1) });
        db.CatalogoVersiones.Add(new CatalogoVersion { Catalogo = "c_Moneda", VersionCatalogo = "4.0", RevisionCatalogo = "prueba",
            FechaCargaUtc = DateTime.UtcNow, RenglonesVigentes = 2 });
        db.SatFormasPago.AddRange(Enumerable.Range(0, 100).Select(x => new SatFormaPago
            { Clave = x.ToString("D2"), Descripcion = "Forma de prueba " + x, Vigente = true, FechaInicioVigencia = new(2020, 1, 1) }));
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (!_baseTemporal.StartsWith("FacturacionPruebasCatalogos_", StringComparison.Ordinal))
            throw new InvalidOperationException("Base de prueba inesperada.");
        await using var db = Abrir();
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Consultas_traducen_a_sql_y_respetan_filtros_paginacion_y_topes()
    {
        await using var db = Abrir();
        var servicio = new ServicioDeCatalogosDeOperador(db);
        var definiciones = await servicio.ListarAsync(default);
        Assert.Equal(CatalogosSatDeConsulta.Disponibles.Count, definiciones.Count);
        Assert.Equal("4.0", definiciones.Single(x => x.Definicion.Clave == "c_Moneda").UltimaCarga?.VersionCatalogo);
        foreach (var catalogo in CatalogosSatDeConsulta.Disponibles)
        {
            var pagina = await servicio.ConsultarAsync(catalogo.Clave, catalogo.RequiereBusqueda ? "100" : "", null, 0, 24, default);
            Assert.True(pagina.EsExito, catalogo.Clave + ": " + pagina.Error?.Mensaje);
            // También fuerza la traducción de las búsquedas sobre las proyecciones particulares.
            if (!catalogo.RequiereBusqueda)
                Assert.True((await servicio.ConsultarAsync(catalogo.Clave, "prueba", null, 0, 24, default)).EsExito, catalogo.Clave);
        }
        var todas = await servicio.ConsultarAsync("c_Moneda", null, null, 0, 24, default);
        Assert.Equal(3, todas.Valor.Total);
        var vigentes = await servicio.ConsultarAsync("c_Moneda", "", true, 0, 24, default);
        Assert.Equal(2, vigentes.Valor.Total);
        Assert.All(vigentes.Valor.Elementos, x => Assert.True(x.Vigente));
        var baja = await servicio.ConsultarAsync("c_Moneda", "", false, 0, 24, default);
        Assert.Equal("ZZZ", Assert.Single(baja.Valor.Elementos).Clave);
        var busqueda = await servicio.ConsultarAsync("c_Moneda", "Peso", null, 0, 24, default);
        Assert.Equal("MXN", Assert.Single(busqueda.Valor.Elementos).Clave);
        var primera = await servicio.ConsultarAsync("c_FormaPago", "", null, 0, 500, default);
        var segunda = await servicio.ConsultarAsync("c_FormaPago", "", null, 1, 500, default);
        Assert.Equal(100, primera.Valor.Total);
        Assert.Equal(50, primera.Valor.Elementos.Count);
        Assert.Empty(primera.Valor.Elementos.Select(x => x.Clave).Intersect(segunda.Valor.Elementos.Select(x => x.Clave)));
        Assert.Equal("texto-corto", (await servicio.ConsultarAsync("c_ClaveProdServ", "12", null, 0, 24, default)).Error?.Codigo);
        Assert.Equal("texto-invalido", (await servicio.ConsultarAsync("c_ClaveProdServ", "\"\"\"", null, 0, 24, default)).Error?.Codigo);
        Assert.Equal("catalogo-invalido", (await servicio.ConsultarAsync("AspNetUsers", "", null, 0, 24, default)).Error?.Codigo);
        Assert.Equal(3, await db.SatMonedas.CountAsync());
    }

    [Fact]
    public async Task Busquedas_por_descripcion_usan_texto_completo_y_devuelven_resultados()
    {
        await using var db = Abrir();
        db.SatClavesProdServ.Add(new SatClaveProdServ { Clave = "30111601", Descripcion = "Cemento de prueba", Vigente = true });
        db.SatCodigosPostales.Add(new SatCodigoPostal { Clave = "42000", ClaveEstado = "HID", ClaveMunicipio = "048", Vigente = true });
        db.SatMunicipios.Add(new SatMunicipio { Clave = "048", ClaveEstado = "HID", Descripcion = "Pachuca", Vigente = true });
        db.SatColonias.Add(new SatColonia { Clave = "0001", ClaveCodigoPostal = "42000", Nombre = "Centro", Vigente = true });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FULLTEXT CATALOG CatalogoTextoPrueba AS DEFAULT;
            CREATE FULLTEXT INDEX ON SatClaveProdServ(Descripcion LANGUAGE 3082, PalabrasSimilares LANGUAGE 3082)
                KEY INDEX PK_SatClaveProdServ ON CatalogoTextoPrueba WITH STOPLIST = SYSTEM, CHANGE_TRACKING AUTO;
            CREATE FULLTEXT INDEX ON SatColonia(Nombre LANGUAGE 3082)
                KEY INDEX IX_SatColonia_IdInterno ON CatalogoTextoPrueba WITH STOPLIST = SYSTEM, CHANGE_TRACKING AUTO;
            CREATE FULLTEXT INDEX ON SatMunicipio(Descripcion LANGUAGE 3082)
                KEY INDEX IX_SatMunicipio_IdInterno ON CatalogoTextoPrueba WITH STOPLIST = SYSTEM, CHANGE_TRACKING AUTO;
            """);
        var servicio = new ServicioDeCatalogosDeOperador(db);
        foreach (var (catalogo, texto) in new[] { ("c_ClaveProdServ", "cemento"), ("c_Colonia", "Centro"),
            ("c_CodigoPostal", "Centro"), ("c_CodigoPostal", "Pachuca") })
        {
            var limite = DateTime.UtcNow.AddSeconds(30);
            while (true)
            {
                var resultado = await servicio.ConsultarAsync(catalogo, texto, true, 0, 24, default);
                Assert.True(resultado.EsExito, resultado.Error?.Mensaje);
                if (resultado.Valor.Total > 0) break;
                Assert.True(DateTime.UtcNow < limite, "No terminó de poblarse el índice de " + catalogo);
                await Task.Delay(250);
            }
        }
    }

    [Fact]
    public async Task Politica_solo_acepta_operador_con_permiso_sin_claims_de_empresa()
    {
        var servicios = new ServiceCollection().AddLogging();
        servicios.AddAuthorizationBuilder().AgregarPoliticasDeOperador();
        using var proveedor = servicios.BuildServiceProvider();
        var autorizacion = proveedor.GetRequiredService<IAuthorizationService>();
        static ClaimsPrincipal Principal(params Claim[] claims) => new(new ClaimsIdentity(claims, "prueba"));
        var operador = new Claim(ClavesDeClaim.Operador, Guid.NewGuid().ToString());
        var permiso = new Claim(ClavesDeClaim.Permiso, PermisosDePanel.VerCatalogosSat);
        Assert.False((await autorizacion.AuthorizeAsync(Principal(operador), null, PoliticasDeOperador.VerCatalogosSat)).Succeeded);
        Assert.True((await autorizacion.AuthorizeAsync(Principal(operador, permiso), null, PoliticasDeOperador.VerCatalogosSat)).Succeeded);
        Assert.True((await autorizacion.AuthorizeAsync(Principal(operador, new(ClavesDeClaim.Permiso, PermisosDePanel.VerConfiguracion)),
            null, PoliticasDeOperador.VerCatalogosSat)).Succeeded);
        Assert.False((await autorizacion.AuthorizeAsync(Principal(operador, permiso, new(ClavesDeClaim.Empresa, Guid.NewGuid().ToString())),
            null, PoliticasDeOperador.VerCatalogosSat)).Succeeded);
        Assert.False((await autorizacion.AuthorizeAsync(Principal(permiso, new(ClavesDeClaim.Usuario, Guid.NewGuid().ToString())),
            null, PoliticasDeOperador.VerCatalogosSat)).Succeeded);
    }
}
