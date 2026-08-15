using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Plataforma.Folios;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Pruebas;

/// <summary>
/// La prueba obligatoria de la fase 4 (PROMPT-FASES-A.md): cincuenta hilos reservando folio
/// de la misma serie a la vez no producen ningún duplicado ni ningún hueco.
///
/// <para><b>Por qué contra SQL Server de verdad</b></para>
/// Lo que se está probando <b>es</b> el bloqueo de renglón del procedimiento almacenado. Un
/// proveedor en memoria no tiene <c>UPDLOCK</c>, no tiene transacciones reales y no tiene
/// contención: pasaría siempre, incluso con la implementación ingenua que este código existe
/// para evitar. Una prueba que no puede fallar no prueba nada.
///
/// <para><b>Qué cubre y qué no</b></para>
/// Cubre la concurrencia dentro de un proceso, que es donde vive el riesgo real: varias
/// peticiones HTTP atendidas a la vez por el mismo servidor. <b>No</b> cubre varias
/// instancias del servidor contra la misma base, pero no hace falta: el bloqueo lo impone la
/// base, no la aplicación, así que el resultado es el mismo venga de donde venga la conexión.
/// Tampoco cubre reintentos por interbloqueo, que aquí no aparecen porque todos los hilos
/// toman el mismo único renglón y en el mismo orden.
/// </summary>
public sealed class ReservaConcurrenteDeFoliosPruebas : IAsyncLifetime
{
    private const string Conexion =
        "Server=.;Database=FacturacionPruebasFolios;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    private const int Hilos = 50;
    private const int FolioInicial = 1;

    private readonly Guid _cuenta = Guid.NewGuid();
    private readonly Guid _empresa = Guid.NewGuid();
    private readonly Guid _serie = Guid.NewGuid();

    private static AppDbContext Contexto(Guid? empresa)
    {
        var tenencia = new ContextoEmpresaFijo(empresa);
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .Configurar(Conexion, tenencia)
            .Options;

        return new AppDbContext(opciones, tenencia);
    }

    private static ServicioDeFolios Servicio(AppDbContext db, Guid empresa)
    {
        var tenencia = new ContextoEmpresaFijo(empresa);
        return new ServicioDeFolios(db, tenencia, new ServicioDeBitacora(db, tenencia, new HttpContextAccessor()));
    }

    public async Task InitializeAsync()
    {
        await using var db = Contexto(null);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        db.Cuentas.Add(new Cuenta
        {
            Id = _cuenta,
            Nombre = "Cuenta de prueba",
            CorreoContacto = "pruebas@ejemplo.mx",
            FechaAltaUtc = DateTime.UtcNow
        });

        db.Empresas.Add(new Empresa
        {
            Id = _empresa,
            CuentaId = _cuenta,
            Rfc = "CBA010101BB2",
            NombreFiscal = "EMPRESA DE PRUEBA",
            RegimenFiscal = "601",
            CodigoPostalExpedicion = "42000",
            ZonaHoraria = "Central Standard Time (Mexico)",
            FechaAltaUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        // La serie va en un contexto con empresa activa: el interceptor de sellado exige
        // saber a qué empresa pertenece cada renglón, y hacerlo así prueba de paso que el
        // aislamiento sigue puesto en este camino.
        await using var conEmpresa = Contexto(_empresa);

        conEmpresa.Series.Add(NuevaSerie(_serie, "A", FolioInicial));

        await conEmpresa.SaveChangesAsync();
    }

    private Serie NuevaSerie(Guid id, string prefijo, int folioInicial) => new()
    {
        Id = id,
        EmpresaId = _empresa,
        Prefijo = prefijo,
        FolioInicial = folioInicial,
        FolioActual = folioInicial - 1,
        TipoComprobante = "I",
        Activa = true,
        FechaAltaUtc = DateTime.UtcNow
    };

    public async Task DisposeAsync()
    {
        await using var db = Contexto(null);
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Cincuenta_hilos_a_la_vez_no_repiten_ni_saltan_folios()
    {
        // Pistoletazo de salida para que los cincuenta pidan folio de verdad al mismo tiempo:
        // sin él, los primeros terminarían antes de que arrancaran los últimos y no habría
        // contención que probar.
        //
        // Es un TaskCompletionSource y no un Barrier a propósito: Barrier.SignalAndWait
        // BLOQUEA el hilo, así que cincuenta tareas esperando en él agotan el grupo de hilos
        // y la barrera no se completa hasta que el grupo va inyectando hilos de a uno. Se
        // descubrió aquí: la prueba se colgaba varios minutos. Esperar el TCS no bloquea
        // ningún hilo, y RunContinuationsAsynchronously evita que las cincuenta
        // continuaciones se ejecuten una tras otra en el hilo que dispara.
        var pistoletazo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tareas = Enumerable.Range(0, Hilos).Select(async _ =>
        {
            // Un contexto por hilo: DbContext no es seguro para varios hilos, y compartirlo
            // haría fallar la prueba por una razón que no es la que se está probando.
            await using var db = Contexto(_empresa);
            var servicio = Servicio(db, _empresa);

            await pistoletazo.Task;

            return await servicio.ReservarAsync(_serie, CancellationToken.None);
        }).ToArray();

        pistoletazo.SetResult();

        var reservas = await Task.WhenAll(tareas);

        var folios = reservas.Select(r => r.Folio).OrderBy(f => f).ToArray();

        Assert.Equal(Hilos, folios.Length);

        // Sin duplicados: dos comprobantes con el mismo folio los rechaza el SAT.
        Assert.Equal(Hilos, folios.Distinct().Count());

        // Sin huecos: la numeración va del inicial al inicial+49, consecutiva.
        Assert.Equal(Enumerable.Range(FolioInicial, Hilos), folios);

        // Todas las reservas quedaron registradas, que es lo que después justifica cada
        // número de la numeración.
        await using var comprobacion = Contexto(_empresa);

        var enBase = await comprobacion.ReservasFolio
            .AsNoTracking()
            .Where(r => r.SerieId == _serie)
            .Select(r => r.Folio)
            .ToListAsync();

        Assert.Equal(Hilos, enBase.Count);
        Assert.Equal(Hilos, enBase.Distinct().Count());

        // Y el contador de la serie quedó donde debe: ni adelantado ni atrasado.
        var folioActual = await comprobacion.Series
            .AsNoTracking()
            .Where(s => s.Id == _serie)
            .Select(s => s.FolioActual)
            .SingleAsync();

        Assert.Equal(FolioInicial + Hilos - 1, folioActual);
    }

    /// <summary>
    /// Un folio abandonado <b>no</b> vuelve a la serie: el siguiente comprobante toma el
    /// número siguiente y el hueco queda justificado en la bitácora (CLAUDE.md §5).
    /// </summary>
    [Fact]
    public async Task Un_folio_abandonado_no_se_recicla()
    {
        await using var db = Contexto(_empresa);
        var servicio = Servicio(db, _empresa);

        var serieId = Guid.NewGuid();
        db.Series.Add(NuevaSerie(serieId, "B", folioInicial: 1));
        await db.SaveChangesAsync();

        var primera = await servicio.ReservarAsync(serieId, CancellationToken.None);
        await servicio.LiberarSiNoUsadoAsync(primera.ReservaId, CancellationToken.None);

        var segunda = await servicio.ReservarAsync(serieId, CancellationToken.None);

        Assert.Equal(1, primera.Folio);
        Assert.Equal(2, segunda.Folio);

        var abandonada = await db.ReservasFolio.AsNoTracking().SingleAsync(r => r.Id == primera.ReservaId);
        Assert.Equal(EstadosDeReservaFolio.Abandonado, abandonada.Estado);
        Assert.NotNull(abandonada.MomentoResolucionUtc);
    }
}
