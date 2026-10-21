using Facturacion.Server.Data;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Instalacion;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Catalogos;

public sealed record AvanceDeCatalogos(bool Listo, string Estado, string Catalogo, int Completados, int Total, string? Referencia = null);

public sealed class EstadoDeCargaDeCatalogos
{
    private AvanceDeCatalogos _avance = new(false, "comprobando", "", 0, PreparacionAutomaticaDeCatalogos.Inventario.Length);
    public AvanceDeCatalogos Obtener() => Volatile.Read(ref _avance);
    public void Actualizar(AvanceDeCatalogos avance) => Volatile.Write(ref _avance, avance);
}

public sealed class PreparacionAutomaticaDeCatalogos(
    IServiceScopeFactory ambitos, EstadoDeCargaDeCatalogos estado, IConfiguration configuracion,
    ILogger<PreparacionAutomaticaDeCatalogos> registro) : BackgroundService
{
    public sealed record Entrada(string Catalogo, string Tabla, string Archivo, string Grupo);

    public static readonly Entrada[] Inventario =
    [
        Base("c_FormaPago", "SatFormaPago"), Base("c_Exportacion", "SatExportacion"),
        Base("c_MetodoPago", "SatMetodoPago"), Base("c_Periodicidad", "SatPeriodicidad"),
        Base("c_Meses", "SatMes"), Base("c_TipoRelacion", "SatTipoRelacion"),
        Base("c_Pais", "SatPais"), Base("c_ObjetoImp", "SatObjetoImp"), Base("c_Moneda", "SatMoneda"),
        Base("c_TipoDeComprobante", "SatTipoDeComprobante"), Base("c_RegimenFiscal", "SatRegimenFiscal"),
        Base("c_UsoCFDI", "SatUsoCfdi"), Base("c_ClaveProdServ", "SatClaveProdServ"),
        Base("c_ClaveUnidad", "SatClaveUnidad"), Base("c_Impuesto", "SatImpuesto"),
        Base("c_TipoFactor", "SatTipoFactor"), Base("c_TasaOCuota", "SatTasaOCuota"),
        Base("c_Estado", "SatEstado"), Base("C_Municipio", "SatMunicipio"),
        Base("C_Colonia", "SatColonia"), Base("c_CodigoPostal", "SatCodigoPostal"),
        Porte("c_ConfigAutotransporte", "SatConfiguracionAutotransporte"),
        Porte("c_TipoPermiso", "SatTipoPermiso"), Porte("c_FiguraTransporte", "SatFiguraTransporte"),
        Porte("c_ClaveProdServCP", "SatClaveProdServCartaPorte"),
        new("c_INCOTERM", "SatIncoterm", "c_INCOTERM20.xls", "comercio"),
        new("c_UnidadAduana", "SatUnidadAduana", "c_UnidadAduana20.xls", "comercio"),
        new("c_FraccionArancelaria", "SatFraccionArancelaria", "c_FraccionArancelaria_20240513.xls", "comercio")
    ];

    private static Entrada Base(string nombre, string tabla) => new(nombre, tabla, "catCFDI_V_4_20260806.xls", "cfdi");
    private static Entrada Porte(string nombre, string tabla) => new(nombre, tabla, "CatalogosCartaPorte31.xls", "porte");

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await using var ambito = ambitos.CreateAsyncScope();
            var db = ambito.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
            await db.Database.OpenConnectionAsync(ct);
            var bloqueo = false;
            try
            {
                while (!bloqueo)
                {
                    await using var comando = db.Database.GetDbConnection().CreateCommand();
                    comando.CommandText = """
                        DECLARE @resultado int;
                        EXEC @resultado=sys.sp_getapplock @Resource=N'Facturacion.CargaInicialSAT.v1',
                            @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0;
                        SELECT @resultado;
                        """;
                    bloqueo = Convert.ToInt32(await comando.ExecuteScalarAsync(ct)) >= 0;
                    if (!bloqueo)
                    {
                        estado.Actualizar(new(false, "esperando", "", 0, Inventario.Length));
                        await Task.Delay(TimeSpan.FromSeconds(3), ct);
                    }
                }

                var versiones = await db.CatalogoVersiones.AsNoTracking().ToListAsync(ct);
                var tablas = await db.Database.SqlQueryRaw<FilasPorTabla>("""
                    SELECT t.name AS Tabla, SUM(p.rows) AS Filas
                    FROM sys.tables t JOIN sys.partitions p ON t.object_id=p.object_id AND p.index_id IN (0,1)
                    WHERE t.name LIKE N'Sat%' GROUP BY t.name
                    """).ToListAsync(ct);
                var cargados = versiones.Where(v => v.RenglonesVigentes > 0
                    && Inventario.Any(i => i.Catalogo.Equals(v.Catalogo, StringComparison.OrdinalIgnoreCase)
                        && tablas.Any(t => t.Tabla == i.Tabla && t.Filas > 0)))
                    .Select(v => v.Catalogo).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var pendientes = Inventario.Where(i => !cargados.Contains(i.Catalogo)).ToArray();
                var completados = Inventario.Length - pendientes.Length;
                estado.Actualizar(new(false, "cargando", "", completados, Inventario.Length));
                var directorio = configuracion["CatalogosSat:Directorio"]
                    ?? Path.Combine(AppContext.BaseDirectory, "CatalogosSAT");
                foreach (var entrada in pendientes)
                    if (!File.Exists(Path.Combine(directorio, entrada.Archivo)))
                        throw new FileNotFoundException("Falta un archivo SAT en la publicación.", entrada.Archivo);

                void Avanzar(string nombre, bool terminado)
                {
                    if (terminado) completados++;
                    estado.Actualizar(new(false, "cargando", nombre, completados, Inventario.Length));
                    registro.LogInformation("Carga SAT: {Catalogo}, completado={Completado}", nombre, terminado);
                }
                var nombres = pendientes.Select(i => i.Catalogo).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (pendientes.Any(i => i.Grupo == "cfdi"))
                    await new ImportadorCatalogosSat(db).ImportarAsync(
                        Path.Combine(directorio, "catCFDI_V_4_20260806.xls"), ct, pendientes: nombres, avance: Avanzar);
                if (pendientes.Any(i => i.Grupo == "porte"))
                    await new ImportadorCatalogosCartaPorte(db).ImportarAsync(
                        Path.Combine(directorio, "CatalogosCartaPorte31.xls"), ct, pendientes: nombres, avance: Avanzar);
                foreach (var entrada in pendientes.Where(i => i.Grupo == "comercio"))
                {
                    Avanzar(entrada.Catalogo, false);
                    await CargaDeCatalogosComercioExteriorCli.ImportarCatalogoAsync(
                        db, entrada.Catalogo, Path.Combine(directorio, entrada.Archivo), ct);
                    Avanzar(entrada.Catalogo, true);
                    db.ChangeTracker.Clear();
                }
                estado.Actualizar(new(true, "listo", "", Inventario.Length, Inventario.Length));
                registro.LogInformation("Catálogos SAT preparados: {Total}; cargados en este arranque: {Nuevos}", Inventario.Length, pendientes.Length);
            }
            finally
            {
                if (bloqueo)
                {
                    await using var comando = db.Database.GetDbConnection().CreateCommand();
                    comando.CommandText = "EXEC sys.sp_releaseapplock @Resource=N'Facturacion.CargaInicialSAT.v1', @LockOwner='Session'";
                    await comando.ExecuteNonQueryAsync(CancellationToken.None);
                }
                await db.Database.CloseConnectionAsync();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception error)
        {
            var referencia = Guid.NewGuid().ToString("N");
            var anterior = estado.Obtener();
            estado.Actualizar(anterior with { Estado = "error", Referencia = referencia });
            registro.LogError(error, "Falló la preparación SAT. Referencia: {Referencia}", referencia);
        }
    }

    public sealed class FilasPorTabla
    {
        public string Tabla { get; set; } = "";
        public long Filas { get; set; }
    }
}

public static class PreparacionDeCatalogosModule
{
    public static IServiceCollection AgregarPreparacionDeCatalogos(this IServiceCollection servicios)
    {
        servicios.AddSingleton<EstadoDeCargaDeCatalogos>();
        servicios.AddHostedService<PreparacionAutomaticaDeCatalogos>();
        return servicios;
    }

    public static WebApplication UsePreparacionDeCatalogos(this WebApplication app)
    {
        app.Use(async (contexto, siguiente) =>
        {
            var ruta = contexto.Request.Path.Value;
            if (contexto.Request.Method == "GET" && ruta is "/instalacion/catalogos.js" or "/instalacion/catalogos.css")
            {
                contexto.Response.Headers.CacheControl = "no-store";
                await InstalacionModule.Recurso(ruta.EndsWith(".js", StringComparison.Ordinal) ? "catalogos.js" : "asistente.css",
                    ruta.EndsWith(".js", StringComparison.Ordinal) ? "text/javascript; charset=utf-8" : "text/css; charset=utf-8").ExecuteAsync(contexto);
                return;
            }
            var avance = contexto.RequestServices.GetRequiredService<EstadoDeCargaDeCatalogos>().Obtener();
            if (contexto.Request.Method == "GET" && ruta == "/api/instalacion/catalogos")
            {
                contexto.Response.Headers.CacheControl = "no-store";
                await contexto.Response.WriteAsJsonAsync(avance, contexto.RequestAborted);
                return;
            }
            if (avance.Listo || ruta == "/api/version") { await siguiente(contexto); return; }
            contexto.Response.Headers.CacheControl = "no-store";
            if (contexto.Request.Method == "GET" && !contexto.Request.Path.StartsWithSegments("/api"))
                await InstalacionModule.Recurso("catalogos.html", "text/html; charset=utf-8").ExecuteAsync(contexto);
            else
            {
                contexto.Response.Headers.RetryAfter = "5";
                await ResultadosDeError.EscribirProblema(contexto, 503, "catalogos-pendientes",
                    "Catálogos no disponibles", avance.Estado == "error" ? "No se pudo completar la carga SAT." : "Preparando catálogos SAT.");
            }
        });
        return app;
    }
}
