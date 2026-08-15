using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Plataforma;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Timbres;

/// <summary>
/// Compra de paquetes, saldo, historial y membresía.
///
/// <para><b>El precio nunca viene del cliente</b></para>
/// <see cref="ComprarAsync"/> recibe un identificador de paquete y nada más. Todo lo que se
/// cobra se lee del catálogo del servidor en el momento de comprar. Es la regla de CLAUDE.md
/// §4 y no tiene excepciones: si el precio viajara en la petición, el usuario podría
/// editarlo, porque el código del cliente corre en su navegador y es legible.
/// </summary>
public sealed class ServicioDeCompras(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno contexto,
    IServicioDeBitacora bitacora)
{
    /// <summary>Aviso de vencimiento de la membresía; margen para que dé tiempo de renovar.</summary>
    private const int DiasDeAvisoDeVencimiento = 30;

    public async Task<IReadOnlyList<PaqueteDto>> PaquetesAsync(CancellationToken ct)
        => await baseDeDatos.Paquetes
            .AsNoTracking()
            .Where(p => p.Activo)
            .OrderBy(p => p.Orden)
            .Select(p => new PaqueteDto(
                p.Id, p.Nombre, p.CantidadTimbres, p.PrecioPorTimbre, p.PrecioTotal, p.VigenciaMeses))
            .ToListAsync(ct);

    public async Task<SaldoTimbresDto> SaldoAsync(CancellationToken ct)
        => await baseDeDatos.BolsasTimbres
               .AsNoTracking()
               .Where(b => b.EmpresaId == contexto.EmpresaId)
               .Select(b => new SaldoTimbresDto(b.Disponibles, b.Reservados))
               .FirstOrDefaultAsync(ct)
           // Una empresa que nunca compró no tiene renglón de bolsa. Es un saldo de cero, no
           // un error: la bolsa nace al acreditarse la primera compra.
           ?? new SaldoTimbresDto(0, 0);

    public async Task<Resultado<CompraDto>> ComprarAsync(PeticionDeCompra peticion, CancellationToken ct)
    {
        var paquete = await baseDeDatos.Paquetes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == peticion.PaqueteId, ct);

        if (paquete is null)
            return ErrorNegocio.NoEncontrado("paquete-no-encontrado", "Ese paquete de timbres no existe.");

        if (!paquete.Activo)
        {
            return ErrorNegocio.Regla(
                "paquete-no-disponible",
                $"El paquete «{paquete.Nombre}» ya no está a la venta. Elige uno de los disponibles.");
        }

        var compra = new CompraTimbres
        {
            Id = Guid.NewGuid(),
            EmpresaId = contexto.EmpresaId,
            PaqueteId = paquete.Id,
            UsuarioId = contexto.UsuarioActual ?? Guid.Empty,

            // Copias, no referencias: lo que se cobra queda congelado aunque el catálogo cambie.
            NombrePaquete = paquete.Nombre,
            CantidadTimbres = paquete.CantidadTimbres,
            PrecioPorTimbre = paquete.PrecioPorTimbre,
            PrecioTotal = paquete.PrecioTotal,
            VigenciaMeses = paquete.VigenciaMeses,

            Estado = EstadosDeCompra.PendienteDePago,
            CreadaUtc = DateTime.UtcNow
        };

        baseDeDatos.ComprasTimbres.Add(compra);

        bitacora.Registrar(
            EntidadesDeBitacora.CompraTimbres, compra.Id.ToString(), AccionesDeBitacora.CompraSolicitada,
            despues: new { compra.NombrePaquete, compra.CantidadTimbres, compra.PrecioTotal });

        await baseDeDatos.SaveChangesAsync(ct);

        return ADto(compra);
    }

    public async Task<IReadOnlyList<CompraDto>> ComprasAsync(CancellationToken ct)
        => await baseDeDatos.ComprasTimbres
            .AsNoTracking()
            .OrderByDescending(c => c.CreadaUtc)
            .Select(c => new CompraDto(
                c.Id, c.NombrePaquete, c.CantidadTimbres, c.PrecioPorTimbre, c.PrecioTotal,
                c.Estado, c.CreadaUtc, c.AcreditadaUtc, c.VenceUtc))
            .ToListAsync(ct);

    /// <param name="tipo">Filtra por <see cref="TiposDeMovimientoTimbre"/>; nulo trae todos.</param>
    public async Task<IReadOnlyList<MovimientoTimbreDto>> MovimientosAsync(
        string? tipo, DateTime? desdeUtc, DateTime? hastaUtc, CancellationToken ct)
    {
        var consulta = baseDeDatos.MovimientosTimbre.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(tipo))
            consulta = consulta.Where(m => m.Tipo == tipo);

        if (desdeUtc is not null)
            consulta = consulta.Where(m => m.MomentoUtc >= desdeUtc);

        if (hastaUtc is not null)
            consulta = consulta.Where(m => m.MomentoUtc <= hastaUtc);

        return await consulta
            .OrderByDescending(m => m.MomentoUtc)
            .Select(m => new MovimientoTimbreDto(
                m.Id, m.Tipo, m.DeltaDisponible, m.DeltaReservado,
                m.DisponiblesDespues, m.Motivo, m.MomentoUtc))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Membresía vigente de la cuenta, o la última que hubo si ya venció.
    /// <para>
    /// La cuenta se resuelve desde el contexto, no desde la petición. <c>Membresia</c> no
    /// lleva <c>EmpresaId</c> y por lo tanto queda fuera del filtro global de EF, así que este
    /// <c>Where</c> por cuenta es la única barrera entre una cuenta y la de al lado.
    /// </para>
    /// </summary>
    public async Task<MembresiaDto?> MembresiaAsync(CancellationToken ct)
    {
        var cuentaId = contexto.CuentaActual;

        if (cuentaId is null) return null;

        var membresia = await baseDeDatos.Membresias
            .AsNoTracking()
            .Where(m => m.CuentaId == cuentaId)
            .OrderByDescending(m => m.FinUtc)
            .FirstOrDefaultAsync(ct);

        if (membresia is null) return null;

        var dias = (int)Math.Ceiling((membresia.FinUtc - DateTime.UtcNow).TotalDays);

        // El estado guardado puede haber quedado viejo: nadie recorre la tabla a medianoche
        // para marcar las vencidas. Se corrige al leer, comparando contra el reloj del
        // servidor, que es el único en el que se puede confiar.
        var estado = membresia.Estado == EstadosDeMembresia.Activa && dias < 0
            ? EstadosDeMembresia.Vencida
            : membresia.Estado;

        return new MembresiaDto(membresia.InicioUtc, membresia.FinUtc, estado, dias);
    }

    /// <summary>Días a partir de los cuales conviene avisar del vencimiento.</summary>
    public static bool EstaPorVencer(MembresiaDto membresia)
        => membresia.Estado == EstadosDeMembresia.Activa
           && membresia.DiasParaVencer <= DiasDeAvisoDeVencimiento;

    /// <summary>
    /// Acredita el pago y mete los timbres a la bolsa. Es el único punto donde se crea saldo.
    /// <para>
    /// No hay endpoint para esto a propósito: lo ejecuta el operador del SaaS por consola
    /// (<c>--acreditar-compra</c>). Un usuario del inquilino no puede acreditar su propia
    /// compra, y todavía no existe una identidad de operador que pudiera protegerlo como
    /// endpoint; inventarla aquí sería adelantarse a la fase 8.
    /// </para>
    /// </summary>
    public async Task<Resultado<CompraDto>> AcreditarAsync(Guid compraId, CancellationToken ct)
    {
        var parametros = new[]
        {
            new SqlParameter("@CompraId", compraId),
            new SqlParameter("@MomentoUtc", DateTime.UtcNow)
        };

        var filas = await baseDeDatos.Database
            .SqlQueryRaw<ResultadoDeAcreditacion>(
                "EXEC dbo.AcreditarCompra @CompraId, @MomentoUtc", parametros)
            .ToListAsync(ct);

        if (!filas.Single().Acreditada)
        {
            return ErrorNegocio.Conflicto(
                "compra-no-acreditable",
                "Esa compra no existe o ya no está pendiente de pago.");
        }

        // IgnoreQueryFilters justificado: la acreditación corre desde consola, sin petición ni
        // empresa activa, así que el filtro global no devolvería ningún renglón.
        var compra = await baseDeDatos.ComprasTimbres
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(c => c.Id == compraId, ct);

        bitacora.Registrar(
            EntidadesDeBitacora.CompraTimbres, compra.Id.ToString(), AccionesDeBitacora.CompraAcreditada,
            despues: new { compra.CantidadTimbres, compra.PrecioTotal, compra.VenceUtc },
            empresaId: compra.EmpresaId);

        await baseDeDatos.SaveChangesAsync(ct);

        return ADto(compra);
    }

    private static CompraDto ADto(CompraTimbres compra) => new(
        compra.Id, compra.NombrePaquete, compra.CantidadTimbres, compra.PrecioPorTimbre,
        compra.PrecioTotal, compra.Estado, compra.CreadaUtc, compra.AcreditadaUtc, compra.VenceUtc);

    private sealed record ResultadoDeAcreditacion(bool Acreditada, int? DisponiblesDespues);
}
