using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Almacen;
using Facturacion.Server.Modules.Plataforma.Catalogos;
using Facturacion.Shared.Operador;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Facturacion.Server.Modules.Operador.Catalogos;

public sealed class ActualizadorDeCatalogos(
    IServiceScopeFactory ambitos, IOptions<OpcionesDeAlmacen> almacen, IWebHostEnvironment entorno,
    IMemoryCache cache, ILogger<ActualizadorDeCatalogos> registro) : BackgroundService
{
    public const long MaximoArchivo = 50L * 1024 * 1024;
    private readonly Channel<(ActualizacionCatalogosDto Estado, string Ruta, Guid Operador)> _cola =
        Channel.CreateBounded<(ActualizacionCatalogosDto, string, Guid)>(1);
    private readonly SemaphoreSlim _entrada = new(1, 1);
    private ActualizacionCatalogosDto? _estado;
    public ActualizacionCatalogosDto? Estado => Volatile.Read(ref _estado);

    public async Task<ActualizacionCatalogosDto?> EncolarAsync(string catalogo, IFormFile archivo, Guid operador, CancellationToken ct)
    {
        if (!await _entrada.WaitAsync(0, ct)) return null;
        string? ruta = null;
        try
        {
            if (Estado?.Estado == "cargando") return null;
            var entrada = PreparacionAutomaticaDeCatalogos.Inventario.Single(x => x.Catalogo == catalogo);
            var raiz = Path.GetFullPath(almacen.Value.Raiz, entorno.ContentRootPath);
            var publico = Path.GetFullPath(entorno.WebRootPath ?? Path.Combine(entorno.ContentRootPath, "wwwroot"));
            if (raiz.Equals(publico, StringComparison.OrdinalIgnoreCase)
                || raiz.StartsWith(publico.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La carga SAT requiere almacenamiento privado.");
            var carpeta = Path.Combine(raiz, "actualizaciones-sat", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(carpeta);
            ruta = Path.Combine(carpeta, entrada.Grupo == "comercio"
                ? entrada.Catalogo + "20" + Path.GetExtension(archivo.FileName).ToLowerInvariant()
                : "catalogo" + Path.GetExtension(archivo.FileName).ToLowerInvariant());
            await using (var destino = new FileStream(ruta, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await archivo.CopyToAsync(destino, ct);
            var nuevo = new ActualizacionCatalogosDto(Guid.NewGuid(), catalogo, "cargando", "Validando y actualizando el catálogo…", DateTime.UtcNow);
            Volatile.Write(ref _estado, nuevo);
            if (!_cola.Writer.TryWrite((nuevo, ruta, operador)))
            {
                Volatile.Write(ref _estado, nuevo with { Estado = "error", Mensaje = "No se pudo iniciar la actualización.", FinUtc = DateTime.UtcNow });
                throw new InvalidOperationException("No se pudo iniciar la actualización.");
            }
            ruta = null;
            return nuevo;
        }
        finally
        {
            if (ruta is not null) Limpiar(ruta);
            _entrada.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var trabajo in _cola.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var ambito = ambitos.CreateAsyncScope();
                var db = ambito.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
                await db.Database.OpenConnectionAsync(stoppingToken);
                // El mismo bloqueo que usa la carga inicial impide dos cargas entre instancias.
                await using var bloqueo = db.Database.GetDbConnection().CreateCommand();
                bloqueo.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'Facturacion.CargaInicialSAT.v1', @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0; SELECT @r;";
                if (Convert.ToInt32(await bloqueo.ExecuteScalarAsync(stoppingToken)) < 0)
                    throw new InvalidOperationException("Ya hay otra carga de catálogos. Inténtalo más tarde.");
                try
                {
                    await ImportarAsync(db, trabajo.Estado.Catalogo, trabajo.Ruta, trabajo.Operador, stoppingToken);
                    cache.Remove("catalogos-sat-precargables");
                }
                finally
                {
                    await using var liberar = db.Database.GetDbConnection().CreateCommand();
                    liberar.CommandText = "EXEC sys.sp_releaseapplock @Resource=N'Facturacion.CargaInicialSAT.v1', @LockOwner='Session';";
                    try { await liberar.ExecuteNonQueryAsync(CancellationToken.None); }
                    catch (System.Data.Common.DbException error) { registro.LogWarning(error, "No se pudo liberar el bloqueo de actualización SAT"); }
                }
                Volatile.Write(ref _estado, trabajo.Estado with { Estado = "listo", Mensaje = "Catálogo actualizado.", FinUtc = DateTime.UtcNow });
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                Volatile.Write(ref _estado, trabajo.Estado with { Estado = "error", Mensaje = "La actualización se interrumpió. Vuelve a cargar el archivo.", FinUtc = DateTime.UtcNow });
            }
            catch (Exception error)
            {
                registro.LogError(error, "Falló la actualización del catálogo {Catalogo}", trabajo.Estado.Catalogo);
                Volatile.Write(ref _estado, trabajo.Estado with { Estado = "error", Mensaje = "No se pudo completar la actualización. Revisa el archivo y la última fecha de carga.", FinUtc = DateTime.UtcNow });
            }
            finally { Limpiar(trabajo.Ruta); }
        }
    }

    public static async Task ImportarAsync(AppDbContext db, string catalogo, string ruta, Guid operador, CancellationToken ct)
    {
        var entrada = PreparacionAutomaticaDeCatalogos.Inventario.Single(x => x.Catalogo == catalogo);
        var anterior = await db.CatalogoVersiones.AsNoTracking().SingleOrDefaultAsync(x => x.Catalogo == catalogo, ct);
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        if (entrada.Grupo == "cfdi") await new ImportadorCatalogosSat(db).ImportarAsync(ruta, ct, catalogo);
        else if (entrada.Grupo == "porte") await new ImportadorCatalogosCartaPorte(db).ImportarAsync(ruta, ct, catalogo);
        else await CargaDeCatalogosComercioExteriorCli.ImportarCatalogoAsync(db, catalogo, ruta, ct);
        var actual = await db.CatalogoVersiones.SingleAsync(x => x.Catalogo == catalogo, ct);
        if (anterior?.FechaPublicacion is { } fecha && actual.FechaPublicacion < fecha)
            throw new InvalidOperationException("El archivo es anterior al catálogo instalado.");
        // El nombre de tabla procede del inventario fijo, nunca del archivo o de la petición.
        await using var contar = db.Database.GetDbConnection().CreateCommand();
        contar.Transaction = transaccion.GetDbTransaction();
        contar.CommandText = $"SELECT COUNT(*) FROM dbo.[{entrada.Tabla}] WHERE Vigente=1";
        actual.RenglonesVigentes = Convert.ToInt32(await contar.ExecuteScalarAsync(ct));
        if (actual.RenglonesVigentes == 0) throw new InvalidOperationException("El archivo no contiene claves vigentes.");
        await using var archivo = File.OpenRead(ruta);
        var huella = Convert.ToHexString(await SHA256.HashDataAsync(archivo, ct));
        db.Bitacora.Add(new RegistroBitacora
        {
            OperadorId = operador, Entidad = "CatalogoSat", EntidadId = catalogo, Accion = "catalogo_actualizado",
            MomentoUtc = DateTime.UtcNow,
            ValorAnterior = JsonSerializer.Serialize(anterior),
            ValorNuevo = JsonSerializer.Serialize(new { actual.VersionCatalogo, actual.RevisionCatalogo, actual.FechaPublicacion, actual.RenglonesVigentes, SHA256 = huella })
        });
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
    }

    private void Limpiar(string ruta)
    {
        try { File.Delete(ruta); Directory.Delete(Path.GetDirectoryName(ruta)!); }
        catch (IOException error) { registro.LogWarning(error, "No se pudo limpiar un archivo temporal SAT"); }
        catch (UnauthorizedAccessException error) { registro.LogWarning(error, "No se pudo limpiar un archivo temporal SAT"); }
    }
}
