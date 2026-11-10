using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Facturacion.Server.Data;
using Facturacion.Server.Infra.Instalacion;
using Facturacion.Server.Modules.Plataforma.Catalogos;
using Facturacion.Shared.Mantenimiento;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Infra.ActualizacionBase;

public sealed record ScriptDeBasePermitido(string Nombre, string Sha256);

public sealed class ActualizadorDeBase : BackgroundService
{
    public const int MaximoArchivo = 2 * 1024 * 1024;
    private readonly IConfiguration _configuracion;
    private readonly IServiceScopeFactory _ambitos;
    private readonly PausaDeBase _pausa;
    private readonly PreparacionAutomaticaDeCatalogos _catalogos;
    private readonly ILogger<ActualizadorDeBase> _registro;
    private readonly string _carpeta;
    private readonly SemaphoreSlim _entrada = new(1, 1);
    private readonly Channel<Guid> _cola = Channel.CreateBounded<Guid>(1);
    private EstadoActualizacionBaseDto? _estado;
    public EstadoActualizacionBaseDto? Estado => Volatile.Read(ref _estado);
    public IReadOnlyList<ScriptDeBasePermitido> Permitidos { get; }

    public ActualizadorDeBase(IConfiguration configuracion, IServiceScopeFactory ambitos, PausaDeBase pausa,
        PreparacionAutomaticaDeCatalogos catalogos, ILogger<ActualizadorDeBase> registro, RutasDeInstalacion rutas)
    {
        _configuracion = configuracion; _ambitos = ambitos; _pausa = pausa; _catalogos = catalogos; _registro = registro;
        _carpeta = Path.Combine(rutas.Interna, "actualizaciones-base");
        using var recurso = typeof(ActualizadorDeBase).Assembly.GetManifestResourceStream(
            "Facturacion.Server.Infra.ActualizacionBase.ScriptsPermitidos.json")
            ?? throw new InvalidOperationException("Falta el registro de scripts de actualización.");
        Permitidos = JsonSerializer.Deserialize<ScriptDeBasePermitido[]>(recurso) ?? [];
        var estado = Path.Combine(_carpeta, "estado.json");
        if (!File.Exists(estado))
        {
            if (File.Exists(Path.Combine(_carpeta, "mantenimiento")))
            {
                _estado = new(Guid.Empty, "", "", "interrumpida", "Hay un mantenimiento pendiente. Revisa el respaldo y vuelve a cargar el script.", "", Guid.Empty, DateTime.UtcNow);
                _ = _pausa.CerrarAsync();
                _ = _catalogos.PausarAsync(CancellationToken.None);
            }
            return;
        }
        try
        {
            _estado = JsonSerializer.Deserialize<EstadoActualizacionBaseDto>(File.ReadAllText(estado));
            if (_estado?.Estado is "en_cola" or "ejecutando")
                Guardar(_estado with { Estado = "interrumpida", Mensaje = "La ejecución se interrumpió. Revisa el respaldo antes de reintentar.", FinUtc = DateTime.UtcNow });
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            _estado = new(Guid.Empty, "", "", "interrumpida", "No se pudo leer el último intento. Revisa cnf antes de continuar.", "", Guid.Empty, DateTime.UtcNow);
        }
        if (_estado?.Estado is "error" or "interrumpida" || File.Exists(Path.Combine(_carpeta, "mantenimiento")))
        {
            _ = _pausa.CerrarAsync();
            _ = _catalogos.PausarAsync(CancellationToken.None);
        }
    }

    private SqlConnection Conexion()
    {
        var cadena = new SqlConnectionStringBuilder(_configuracion.GetConnectionString("BaseDeDatos"))
        { Pooling = false, ConnectTimeout = 15 };
        return new(cadena.ConnectionString);
    }

    public async Task<RevisionBaseDto> RevisarBaseAsync(CancellationToken ct)
    {
        await using var conexion = Conexion();
        await conexion.OpenAsync(ct);
        await using var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),DB_NAME(),
                CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion')),
                CASE WHEN DB_ID()>4 THEN 1 ELSE 0 END,
                CASE WHEN TRY_CONVERT(int,LEFT(CONVERT(varchar(128),SERVERPROPERTY('ProductVersion')),
                    CHARINDEX('.',CONVERT(varchar(128),SERVERPROPERTY('ProductVersion'))+'.')-1))>=12 THEN 1 ELSE 0 END,
                ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER'),0),
                ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CREATE TABLE'),0),
                ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CREATE PROCEDURE'),0),
                ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','VIEW DEFINITION'),0),
                ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CREATE FULLTEXT CATALOG'),0),
                ISNULL(FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'),0);
            """;
        await using var lector = await comando.ExecuteReaderAsync(ct);
        await lector.ReadAsync(ct);
        string[] requisitos = [
            "Seleccionar una base de usuario; no se permiten bases de sistema.",
            "Usar SQL Server 2014 o posterior.",
            "Conceder ALTER al usuario de la conexión en esta base.",
            "Conceder CREATE TABLE al usuario de la conexión en esta base.",
            "Conceder CREATE PROCEDURE al usuario de la conexión en esta base.",
            "Conceder VIEW DEFINITION al usuario de la conexión en esta base.",
            "Conceder CREATE FULLTEXT CATALOG al usuario de la conexión en esta base.",
            "Instalar el componente de búsqueda de texto completo (Full-Text) en esta instancia de SQL Server."
        ];
        var faltantes = requisitos.Where((_, i) => lector.GetInt32(i + 3) != 1).ToArray();
        return new(lector.GetString(0), lector.GetString(1), lector.GetString(2), faltantes.Length == 0,
            Permitidos.Select(x => x.Nombre).ToArray(), Estado) { RequisitosFaltantes = faltantes };
    }

    public async Task<EstadoActualizacionBaseDto> PrepararAsync(byte[] contenido, Guid operador, CancellationToken ct)
    {
        if (contenido.Length is 0 or > MaximoArchivo) throw new InvalidDataException("Selecciona un script SQL de hasta 2 MB.");
        var hash = Convert.ToHexString(SHA256.HashData(contenido));
        var aprobado = Permitidos.FirstOrDefault(x => x.Sha256.Equals(hash, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("Este script no está registrado para esta versión del sistema.");
        var lotes = LotesDeSql.Separar(Decodificar(contenido));
        var revision = await RevisarBaseAsync(ct);
        if (!revision.PuedeActualizar)
            throw new InvalidDataException("No se puede revisar el script. " + string.Join(" ", revision.RequisitosFaltantes));
        await _entrada.WaitAsync(ct);
        try
        {
            if (Estado?.Estado is "en_cola" or "ejecutando") throw new InvalidDataException("Ya hay una actualización en curso.");
            // Un fallo previo mantiene la pausa aunque se cargue otro script.
            var bloqueada = Estado?.Estado is "error" or "interrumpida";
            var nuevo = new EstadoActualizacionBaseDto(Guid.NewGuid(), aprobado.Nombre, hash, "preparada",
                "Script revisado. Confirma el respaldo y tu contraseña para ejecutarlo.", revision.Base, operador, DateTime.UtcNow,
                TotalLotes: lotes.Count, Servidor: revision.Servidor);
            Directory.CreateDirectory(_carpeta);
            await File.WriteAllBytesAsync(RutaScript(nuevo.Id), contenido, ct);
            Guardar(nuevo);
            // La marca evita abrir el sistema si IIS se recicla antes de ejecutar de nuevo.
            if (bloqueada) File.WriteAllText(Path.Combine(_carpeta, "mantenimiento"), "pendiente");
            return nuevo;
        }
        finally { _entrada.Release(); }
    }

    public async Task<EstadoActualizacionBaseDto> EncolarAsync(Guid id, Guid operador, CancellationToken ct)
    {
        await _entrada.WaitAsync(ct);
        try
        {
            var estado = Estado;
            if (estado?.Id != id || estado.UsuarioId != operador || estado.Estado != "preparada")
                throw new InvalidDataException("Vuelve a cargar y revisar el script antes de ejecutar.");
            // La pausa se activa antes de responder; la ejecución no depende de la pestaña.
            _ = _pausa.CerrarAsync();
            File.WriteAllText(Path.Combine(_carpeta, "mantenimiento"), "pendiente");
            Guardar(estado with { Estado = "en_cola", Mensaje = "Esperando a que terminen las operaciones abiertas…", InicioUtc = DateTime.UtcNow });
            if (!_cola.Writer.TryWrite(id)) throw new InvalidOperationException("No se pudo iniciar la actualización.");
            return Estado!;
        }
        finally { _entrada.Release(); }
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (File.Exists(Path.Combine(_carpeta, "mantenimiento"))) _ = _pausa.CerrarAsync();
        try
        {
            await foreach (var id in _cola.Reader.ReadAllAsync(ct)) await EjecutarAsync(id, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private async Task EjecutarAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await _catalogos.PausarAsync(ct);
            await _pausa.CerrarAsync().WaitAsync(TimeSpan.FromMinutes(1), ct);
            await using var candado = new FileStream(Path.Combine(_carpeta, "ejecucion.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            var estado = Estado!;
            if (!Permitidos.Any(x => x.Sha256.Equals(estado.Sha256, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("El script ya no está registrado para esta versión. Vuelve a cargar el original.");
            var contenido = await File.ReadAllBytesAsync(RutaScript(id), ct);
            if (Convert.ToHexString(SHA256.HashData(contenido)) != estado.Sha256)
                throw new InvalidDataException("El archivo cambió después de revisarlo. Vuelve a cargar el original.");
            await using var conexion = Conexion();
            await conexion.OpenAsync(ct);
            await using (var destino = conexion.CreateCommand())
            {
                // Se compara el nombre real de SQL, igual que al revisar, no Initial Catalog de la conexión.
                destino.CommandText = "SELECT DB_NAME(), CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))";
                await using var lector = await destino.ExecuteReaderAsync(ct);
                await lector.ReadAsync(ct);
                if (!string.Equals(lector.GetString(0), estado.Base, StringComparison.Ordinal))
                    throw new InvalidDataException("La conexión cambió de base. Vuelve a revisar el script.");
                if (!string.Equals(lector.GetString(1), estado.Servidor, StringComparison.Ordinal))
                    throw new InvalidDataException("La conexión cambió de servidor. Vuelve a revisar el script.");
            }
            await using var bloqueo = conexion.CreateCommand();
            bloqueo.CommandText = """
                DECLARE @r int;
                EXEC @r=sys.sp_getapplock @Resource=N'Facturacion.ActualizacionWeb',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0;
                IF @r<0 THROW 51300,N'Otra actualización está en curso.',1;
                EXEC @r=sys.sp_getapplock @Resource=N'Facturacion.CargaInicialSAT.v1',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0;
                IF @r<0 THROW 51301,N'Hay una carga SAT en curso.',1;
                """;
            await bloqueo.ExecuteNonQueryAsync(ct);
            var lotes = LotesDeSql.Separar(Decodificar(contenido));
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeSpan.FromMinutes(30));
            for (var i = 0; i < lotes.Count; i++)
            {
                Guardar(Estado! with { Estado = "ejecutando", LoteActual = i + 1, Mensaje = $"Ejecutando lote {i + 1} de {lotes.Count}…" });
                await using var comando = conexion.CreateCommand();
                comando.CommandTimeout = 600;
                comando.CommandText = lotes[i];
                await comando.ExecuteNonQueryAsync(limite.Token);
            }
            await using var comprobar = conexion.CreateCommand();
            comprobar.CommandText = "SELECT @@TRANCOUNT";
            if (Convert.ToInt32(await comprobar.ExecuteScalarAsync(ct)) != 0)
                throw new InvalidDataException("El script dejó una transacción abierta. Se cancelaron sus cambios pendientes.");
            // FULLTEXT requiere salir de las transacciones; el script administra sus propios bloques.
            await conexion.CloseAsync();
            await using var ambito = _ambitos.CreateAsyncScope();
            var db = ambito.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!await VerificacionDeEsquema.CoincideAsync(db, ct))
                throw new InvalidDataException("El script terminó, pero el esquema aún no coincide con esta versión. Conserva el respaldo y revisa la base.");
            Guardar(Estado! with { Estado = "completada", Mensaje = "Base actualizada. Comprobando catálogos SAT…", FinUtc = DateTime.UtcNow });
            File.Delete(Path.Combine(_carpeta, "mantenimiento"));
            _catalogos.SolicitarRevision();
            _pausa.Abrir();
        }
        catch (Exception error)
        {
            var mensaje = error switch
            {
                SqlException { Number: 51301 } => "Hay una carga SAT en curso. Espera a que termine y vuelve a cargar el script.",
                SqlException sql => $"La actualización se detuvo (SQL {sql.Number}, línea {sql.LineNumber}). Conserva el respaldo y revisa el esquema antes de reintentar.",
                InvalidDataException => error.Message,
                OperationCanceledException => "La actualización se interrumpió. Revisa la base antes de reintentar.",
                TimeoutException => "Hay operaciones abiertas. Espera a que terminen antes de reintentar.",
                _ => "No se pudo completar la actualización. Revisa los permisos de cnf y conserva el respaldo."
            };
            // No se registra el SQL ni los mensajes del motor: pueden incluir datos del sistema.
            _registro.LogWarning("Actualización de base {Id}: {Tipo}, código SQL {Numero}", id, error.GetType().Name, (error as SqlException)?.Number);
            try { Guardar(Estado! with { Estado = "error", Mensaje = mensaje, FinUtc = DateTime.UtcNow }); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Volatile.Write(ref _estado, Estado! with { Estado = "error", Mensaje = mensaje, FinUtc = DateTime.UtcNow });
                _registro.LogWarning("No se pudo guardar el resultado de actualización {Id} en cnf.", id);
            }
        }
    }

    private string RutaScript(Guid id) => Path.Combine(_carpeta, id.ToString("N") + ".sql");
    private static string Decodificar(byte[] contenido)
    {
        using var lector = new StreamReader(new MemoryStream(contenido), new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        return lector.ReadToEnd();
    }

    private void Guardar(EstadoActualizacionBaseDto estado)
    {
        Directory.CreateDirectory(_carpeta);
        var json = JsonSerializer.Serialize(estado);
        var temporal = Path.Combine(_carpeta, "estado.tmp");
        File.WriteAllText(temporal, json);
        File.Move(temporal, Path.Combine(_carpeta, "estado.json"), overwrite: true);
        File.AppendAllText(Path.Combine(_carpeta, "historial.jsonl"), json + Environment.NewLine);
        Volatile.Write(ref _estado, estado);
    }
}
