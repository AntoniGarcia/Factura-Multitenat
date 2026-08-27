using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Server.Modules.Plataforma.Timbres;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Pruebas;

/// <summary>
/// Las pruebas obligatorias de la fase 7 (PROMPT-FASES-A.md): aritmética de la bolsa y
/// concurrencia al reservar el último timbre.
///
/// <para><b>Por qué contra SQL Server de verdad</b></para>
/// Igual que en folios, lo que se prueba <b>es</b> el bloqueo de renglón del procedimiento
/// almacenado. Un proveedor en memoria no tiene <c>UPDLOCK</c> ni contención real: pasaría
/// aunque el saldo se descontara con la implementación ingenua que este código existe para
/// evitar.
///
/// <para><b>La invariante que se comprueba en cada paso</b></para>
/// <c>SUM(DeltaDisponible) = Bolsa.Disponibles</c> y <c>SUM(DeltaReservado) =
/// Bolsa.Reservados</c>. Es lo que sostiene que el saldo sea la suma de los movimientos y no
/// un número que alguien edita: si un procedimiento moviera el contador sin dejar movimiento
/// —o al revés— esta comprobación lo caza.
/// </summary>
public sealed class AritmeticaDeLaBolsaPruebas : IAsyncLifetime
{
    private const string Conexion =
        "Server=.;Database=FacturacionPruebasTimbres;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    private static readonly Guid PaqueteDe500 = new("9c1f0a10-0000-4000-8000-000000000001");

    private readonly Guid _cuenta = Guid.NewGuid();
    private readonly Guid _empresa = Guid.NewGuid();

    private static AppDbContext Contexto(Guid? empresa)
    {
        var tenencia = new ContextoEmpresaFijo(empresa);
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .Configurar(Conexion, tenencia)
            .Options;

        return new AppDbContext(opciones, tenencia);
    }

    private static ServicioDeTimbres Timbres(AppDbContext db, Guid empresa)
    {
        var tenencia = new ContextoEmpresaFijo(empresa);
        return new ServicioDeTimbres(db, tenencia, new ServicioDeBitacora(db, tenencia, ContextoDeOperadorFijo.SinOperador, new HttpContextAccessor()));
    }

    private static ServicioDeCompras Compras(AppDbContext db, Guid empresa)
    {
        var tenencia = new ContextoEmpresaFijo(empresa);
        return new ServicioDeCompras(db, tenencia, new ServicioDeBitacora(db, tenencia, ContextoDeOperadorFijo.SinOperador, new HttpContextAccessor()));
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
    }

    public async Task DisposeAsync()
    {
        await using var db = Contexto(null);
        await db.Database.EnsureDeletedAsync();
    }

    /// <summary>
    /// La secuencia que pide el prompt: compra, reserva, confirmación, reserva, devolución.
    /// Se comprueba el saldo <b>y</b> la invariante contable después de cada paso, no solo al
    /// final: un error que se compensara a sí mismo pasaría desapercibido mirando solo el
    /// total.
    /// </summary>
    [Fact]
    public async Task Compra_reserva_confirma_reserva_devuelve_deja_el_saldo_correcto()
    {
        await using var db = Contexto(_empresa);
        var timbres = Timbres(db, _empresa);
        var compras = Compras(db, _empresa);

        // Comprar no da timbres: la compra nace pendiente de pago.
        var compra = await compras.ComprarAsync(new PeticionDeCompra(PaqueteDe500), CancellationToken.None);

        Assert.True(compra.EsExito);
        Assert.Equal(EstadosDeCompra.PendienteDePago, compra.Valor.Estado);
        await ComprobarSaldo(disponibles: 0, reservados: 0);

        // Acreditar el pago sí: es el único punto donde se crea saldo.
        var acreditada = await compras.AcreditarAsync(compra.Valor.Id, CancellationToken.None);

        Assert.True(acreditada.EsExito);
        await ComprobarSaldo(disponibles: 500, reservados: 0);

        // Reservar mueve el timbre de disponible a reservado; el total no cambia.
        var primera = await timbres.ReservarAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(499, primera.DisponiblesDespues);
        await ComprobarSaldo(disponibles: 499, reservados: 1);

        // Confirmar lo gasta: baja reservado y no vuelve a disponible.
        await timbres.ConfirmarAsync(primera.ReservaId, CancellationToken.None);
        await ComprobarSaldo(disponibles: 499, reservados: 0);

        var segunda = await timbres.ReservarAsync(Guid.NewGuid(), CancellationToken.None);
        await ComprobarSaldo(disponibles: 498, reservados: 1);

        // Devolver lo regresa: a diferencia del folio, el timbre sí vuelve.
        await timbres.DevolverAsync(segunda.ReservaId, "El PAC rechazó el comprobante", CancellationToken.None);
        await ComprobarSaldo(disponibles: 499, reservados: 0);

        // 500 comprados, uno gastado.
        Assert.Equal(499, await timbres.DisponiblesAsync(CancellationToken.None));

        await using var comprobacion = Contexto(_empresa);

        var reservas = await comprobacion.ReservasTimbre.AsNoTracking().ToListAsync();

        Assert.Equal(EstadosDeReservaTimbre.Confirmado,
            reservas.Single(r => r.Id == primera.ReservaId).Estado);

        var devuelta = reservas.Single(r => r.Id == segunda.ReservaId);
        Assert.Equal(EstadosDeReservaTimbre.Devuelto, devuelta.Estado);
        Assert.Equal("El PAC rechazó el comprobante", devuelta.MotivoResolucion);

        // Los cinco movimientos de la secuencia, cada uno con su tipo.
        var tipos = await comprobacion.MovimientosTimbre
            .AsNoTracking()
            .OrderBy(m => m.MomentoUtc)
            .Select(m => m.Tipo)
            .ToListAsync();

        Assert.Equal(
            [TiposDeMovimientoTimbre.Compra, TiposDeMovimientoTimbre.Reserva,
             TiposDeMovimientoTimbre.Consumo, TiposDeMovimientoTimbre.Reserva,
             TiposDeMovimientoTimbre.Devolucion],
            tipos);
    }

    /// <summary>
    /// Reservar sin saldo devuelve un error de negocio con código estable, no una falla del
    /// sistema: quedarse sin timbres es un hecho previsto del negocio.
    /// </summary>
    [Fact]
    public async Task Sin_saldo_la_reserva_falla_con_error_de_negocio()
    {
        await using var db = Contexto(_empresa);
        var timbres = Timbres(db, _empresa);

        var excepcion = await Assert.ThrowsAsync<ErrorDeNegocioExcepcion>(
            () => timbres.ReservarAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("sin-timbres", excepcion.Error.Codigo);
        Assert.Equal(TipoErrorNegocio.LimiteExcedido, excepcion.Error.Tipo);

        // Y no dejó rastro: sin timbre que apartar, no hay reserva ni movimiento.
        await using var comprobacion = Contexto(_empresa);
        Assert.Empty(await comprobacion.ReservasTimbre.AsNoTracking().ToListAsync());
        Assert.Empty(await comprobacion.MovimientosTimbre.AsNoTracking().ToListAsync());
    }

    /// <summary>
    /// Confirmar dos veces la misma reserva gastaría dos timbres por un solo comprobante. El
    /// estado esperado va en el <c>WHERE</c> del procedimiento, así que la segunda no
    /// encuentra renglón.
    /// </summary>
    [Fact]
    public async Task Confirmar_dos_veces_la_misma_reserva_no_gasta_dos_timbres()
    {
        await using var db = Contexto(_empresa);
        var timbres = Timbres(db, _empresa);

        await AcreditarUnPaquete(db);

        var reserva = await timbres.ReservarAsync(Guid.NewGuid(), CancellationToken.None);
        await timbres.ConfirmarAsync(reserva.ReservaId, CancellationToken.None);

        var excepcion = await Assert.ThrowsAsync<ErrorDeNegocioExcepcion>(
            () => timbres.ConfirmarAsync(reserva.ReservaId, CancellationToken.None));

        Assert.Equal("reserva-de-timbre-no-vigente", excepcion.Error.Codigo);

        await ComprobarSaldo(disponibles: 499, reservados: 0);
    }

    /// <summary>
    /// Acreditar dos veces la misma compra regalaría un paquete entero. Mismo candado: el
    /// estado esperado viaja en el <c>WHERE</c>.
    /// </summary>
    [Fact]
    public async Task Acreditar_dos_veces_la_misma_compra_no_regala_timbres()
    {
        await using var db = Contexto(_empresa);
        var compras = Compras(db, _empresa);

        var compra = await compras.ComprarAsync(new PeticionDeCompra(PaqueteDe500), CancellationToken.None);

        var primera = await compras.AcreditarAsync(compra.Valor.Id, CancellationToken.None);
        var segunda = await compras.AcreditarAsync(compra.Valor.Id, CancellationToken.None);

        Assert.True(primera.EsExito);
        Assert.True(segunda.EsFallo);
        Assert.Equal("compra-no-acreditable", segunda.Error!.Codigo);

        await ComprobarSaldo(disponibles: 500, reservados: 0);
    }

    /// <summary>
    /// La prueba de concurrencia obligatoria: dos hilos peleando por el último timbre. Uno
    /// gana y el otro recibe el error de negocio; el saldo nunca queda en negativo.
    /// <para>
    /// Se corre con muchos pares y no con uno solo: una carrera que se pierde una vez de cada
    /// cien no se ve en un intento, y una prueba que solo falla a veces es peor que ninguna.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Dos_hilos_por_el_ultimo_timbre_no_dejan_saldo_negativo()
    {
        const int Rondas = 25;

        await using var preparacion = Contexto(_empresa);
        await AcreditarUnPaquete(preparacion);

        var ganados = 0;
        var perdidos = 0;

        for (var ronda = 0; ronda < Rondas; ronda++)
        {
            // Exactamente un timbre disponible al empezar cada ronda: el escenario del prompt.
            // Se reajusta en cada vuelta porque el timbre que gana el ganador se queda en
            // reservado y no vuelve solo.
            await DejarDisponiblesEnUno();

            // Pistoletazo por la misma razón que en la prueba de folios: sin él el primero
            // termina antes de que arranque el segundo y no hay contención que probar.
            var pistoletazo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var tareas = Enumerable.Range(0, 2).Select(async _ =>
            {
                // Un contexto por hilo: DbContext no es seguro para varios hilos.
                await using var db = Contexto(_empresa);
                var timbres = Timbres(db, _empresa);

                await pistoletazo.Task;

                try
                {
                    await timbres.ReservarAsync(Guid.NewGuid(), CancellationToken.None);
                    return true;
                }
                catch (ErrorDeNegocioExcepcion excepcion) when (excepcion.Error.Codigo == "sin-timbres")
                {
                    return false;
                }
            }).ToArray();

            pistoletazo.SetResult();

            var resultados = await Task.WhenAll(tareas);

            ganados += resultados.Count(r => r);
            perdidos += resultados.Count(r => !r);

            // Exactamente uno de los dos se llevó el timbre en cada ronda.
            Assert.Equal(1, resultados.Count(r => r));
        }

        Assert.Equal(Rondas, ganados);
        Assert.Equal(Rondas, perdidos);

        // Lo que de verdad importa: el saldo nunca cruzó el cero.
        await using var comprobacion = Contexto(_empresa);
        var bolsa = await comprobacion.BolsasTimbres.AsNoTracking().SingleAsync(b => b.EmpresaId == _empresa);

        Assert.Equal(0, bolsa.Disponibles);
        Assert.Equal(Rondas, bolsa.Reservados);
        Assert.True(bolsa.Disponibles >= 0, "El saldo disponible nunca puede quedar en negativo.");

        await ComprobarInvariante();
    }

    private async Task AcreditarUnPaquete(AppDbContext db)
    {
        var compras = Compras(db, _empresa);
        var compra = await compras.ComprarAsync(new PeticionDeCompra(PaqueteDe500), CancellationToken.None);

        await compras.AcreditarAsync(compra.Valor.Id, CancellationToken.None);
    }

    /// <summary>
    /// Deja la bolsa con exactamente un timbre disponible, compensando la diferencia con un
    /// movimiento de ajuste. Se hace con un movimiento y no editando el contador a secas
    /// justamente porque editar el contador sin dejar rastro es lo que el modelo prohíbe:
    /// hasta la preparación de la prueba respeta la invariante, y así el chequeo final sigue
    /// siendo válido.
    /// </summary>
    private async Task DejarDisponiblesEnUno()
    {
        await using var ajuste = Contexto(_empresa);

        var bolsa = await ajuste.BolsasTimbres.SingleAsync(b => b.EmpresaId == _empresa);

        var delta = 1 - bolsa.Disponibles;

        if (delta == 0) return;

        bolsa.Disponibles += delta;
        bolsa.ActualizadaUtc = DateTime.UtcNow;

        ajuste.MovimientosTimbre.Add(new MovimientoTimbre
        {
            Id = Guid.NewGuid(),
            EmpresaId = _empresa,
            Tipo = TiposDeMovimientoTimbre.Ajuste,
            DeltaDisponible = delta,
            DeltaReservado = 0,
            DisponiblesDespues = bolsa.Disponibles,
            ReservadosDespues = bolsa.Reservados,
            Motivo = "Preparación de la prueba de concurrencia",
            MomentoUtc = DateTime.UtcNow
        });

        await ajuste.SaveChangesAsync();
    }

    /// <summary>
    /// Una empresa que nunca acreditó una compra no tiene renglón de bolsa: eso es un saldo
    /// de cero, no una falla. Por eso se compara contra el par (0,0) en vez de exigir que el
    /// renglón exista.
    /// </summary>
    private async Task ComprobarSaldo(int disponibles, int reservados)
    {
        await using var db = Contexto(_empresa);

        var bolsa = await db.BolsasTimbres.AsNoTracking().FirstOrDefaultAsync(b => b.EmpresaId == _empresa);

        Assert.Equal(disponibles, bolsa?.Disponibles ?? 0);
        Assert.Equal(reservados, bolsa?.Reservados ?? 0);

        await ComprobarInvariante();
    }

    /// <summary>El saldo tiene que ser la suma de los movimientos, en las dos columnas.</summary>
    private async Task ComprobarInvariante()
    {
        await using var db = Contexto(_empresa);

        var bolsa = await db.BolsasTimbres.AsNoTracking().FirstOrDefaultAsync(b => b.EmpresaId == _empresa);

        var sumaDisponible = await db.MovimientosTimbre.AsNoTracking().SumAsync(m => m.DeltaDisponible);
        var sumaReservado = await db.MovimientosTimbre.AsNoTracking().SumAsync(m => m.DeltaReservado);

        Assert.Equal(sumaDisponible, bolsa?.Disponibles ?? 0);
        Assert.Equal(sumaReservado, bolsa?.Reservados ?? 0);
    }
}
