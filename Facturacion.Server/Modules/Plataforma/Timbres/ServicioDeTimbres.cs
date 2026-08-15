using Facturacion.Server.Data;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Errores;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Comun;
using Facturacion.Shared.Contratos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Timbres;

/// <summary>
/// Implementación de <see cref="IServicioTimbres"/> del contrato congelado.
///
/// <para><b>Por qué las tres operaciones son procedimientos y no código C#</b></para>
/// Es el mismo razonamiento que la reserva de folios. Leer el saldo, comprobar que alcanza y
/// descontarlo desde la aplicación deja una ventana entre la comprobación y el descuento en
/// la que otra petición hace lo mismo: las dos leen «queda uno», las dos creen que ganaron, y
/// el saldo termina en −1. Dentro del procedimiento la comprobación viaja en el <c>WHERE</c>
/// del <c>UPDATE</c>, así que comprobar y descontar son la misma operación y la ventana no
/// existe.
///
/// <para><b>Ninguna llamada HTTP dentro de estas transacciones</b></para>
/// CLAUDE.md §5. La transacción que aparta el timbre abre y cierra dentro del procedimiento,
/// antes de que nadie hable con el PAC. Quien timbra reserva, cuelga el teléfono con la base
/// de datos, y solo entonces sale a la red.
/// </summary>
public sealed class ServicioDeTimbres(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno contexto,
    IServicioDeBitacora bitacora) : IServicioTimbres
{
    public async Task<ReservaTimbreDto> ReservarAsync(Guid comprobanteId, CancellationToken ct)
    {
        var reservaId = Guid.NewGuid();
        var empresaId = contexto.EmpresaId;

        var parametros = new[]
        {
            new SqlParameter("@EmpresaId", empresaId),
            new SqlParameter("@ReservaId", reservaId),
            new SqlParameter("@ComprobanteId", comprobanteId),
            new SqlParameter("@MomentoUtc", DateTime.UtcNow)
        };

        var filas = await baseDeDatos.Database
            .SqlQueryRaw<ResultadoDeReserva>(
                "EXEC dbo.ReservarTimbre @EmpresaId, @ReservaId, @ComprobanteId, @MomentoUtc", parametros)
            .ToListAsync(ct);

        var resultado = filas.Single();

        if (!resultado.Reservado)
        {
            // Quedarse sin timbres es un hecho de negocio previsto, no una falla: el contrato
            // pide un error claro y el tipo LimiteExcedido existe exactamente para esto.
            throw new ErrorDeNegocioExcepcion(ErrorNegocio.LimiteExcedido(
                "sin-timbres",
                "No quedan timbres disponibles en esta empresa. Compra un paquete para seguir facturando."));
        }

        bitacora.Registrar(
            EntidadesDeBitacora.ReservaTimbre, reservaId.ToString(), AccionesDeBitacora.TimbreReservado,
            despues: new { comprobanteId, resultado.DisponiblesDespues });

        await baseDeDatos.SaveChangesAsync(ct);

        return new ReservaTimbreDto(reservaId, resultado.DisponiblesDespues);
    }

    public async Task ConfirmarAsync(Guid reservaId, CancellationToken ct)
    {
        var resuelto = await Resolver(
            "EXEC dbo.ConfirmarTimbre @EmpresaId, @ReservaId, @MomentoUtc",
            [
                new SqlParameter("@EmpresaId", contexto.EmpresaId),
                new SqlParameter("@ReservaId", reservaId),
                new SqlParameter("@MomentoUtc", DateTime.UtcNow)
            ], ct);

        if (!resuelto)
        {
            // La reserva no existe, es de otra empresa, o ya se resolvió. Confirmar dos veces
            // gastaría dos timbres por un solo comprobante, así que se falla en vez de callar.
            throw new ErrorDeNegocioExcepcion(ErrorNegocio.Conflicto(
                "reserva-de-timbre-no-vigente",
                "Esa reserva de timbre ya no está apartada: se confirmó o se devolvió antes."));
        }

        bitacora.Registrar(
            EntidadesDeBitacora.ReservaTimbre, reservaId.ToString(), AccionesDeBitacora.TimbreConsumido);

        await baseDeDatos.SaveChangesAsync(ct);
    }

    public async Task DevolverAsync(Guid reservaId, string motivo, CancellationToken ct)
    {
        var resuelto = await Resolver(
            "EXEC dbo.DevolverTimbre @EmpresaId, @ReservaId, @Motivo, @MomentoUtc",
            [
                new SqlParameter("@EmpresaId", contexto.EmpresaId),
                new SqlParameter("@ReservaId", reservaId),
                new SqlParameter("@Motivo", Recortar(motivo)),
                new SqlParameter("@MomentoUtc", DateTime.UtcNow)
            ], ct);

        if (!resuelto)
        {
            throw new ErrorDeNegocioExcepcion(ErrorNegocio.Conflicto(
                "reserva-de-timbre-no-vigente",
                "Esa reserva de timbre ya no está apartada: se confirmó o se devolvió antes."));
        }

        bitacora.Registrar(
            EntidadesDeBitacora.ReservaTimbre, reservaId.ToString(), AccionesDeBitacora.TimbreDevuelto,
            despues: new { motivo });

        await baseDeDatos.SaveChangesAsync(ct);
    }

    public async Task<int> DisponiblesAsync(CancellationToken ct)
        => await baseDeDatos.BolsasTimbres
            .AsNoTracking()
            .Where(b => b.EmpresaId == contexto.EmpresaId)
            .Select(b => b.Disponibles)
            .FirstOrDefaultAsync(ct);

    private async Task<bool> Resolver(string sql, SqlParameter[] parametros, CancellationToken ct)
    {
        var filas = await baseDeDatos.Database
            .SqlQueryRaw<ResultadoDeResolucion>(sql, parametros)
            .ToListAsync(ct);

        return filas.Single().Resuelto;
    }

    /// <summary>El motivo cabe en 300 caracteres; un mensaje de error del PAC puede pasarse.</summary>
    internal static string Recortar(string motivo)
        => motivo.Length <= 300 ? motivo : motivo[..300];

    private sealed record ResultadoDeReserva(bool Reservado, int DisponiblesDespues);

    private sealed record ResultadoDeResolucion(bool Resuelto);
}
