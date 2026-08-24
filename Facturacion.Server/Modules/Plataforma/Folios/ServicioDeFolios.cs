using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma;
using Facturacion.Server.Infra.Bitacora;
using Facturacion.Server.Infra.Tenencia;
using Facturacion.Shared.Contratos;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Folios;

/// <summary>
/// Implementación de <see cref="IServicioFolios"/> del contrato congelado.
///
/// <para><b>Por qué la reserva no está escrita en C#</b></para>
/// Leer <c>FolioActual</c>, sumarle uno y guardarlo desde la aplicación deja una ventana en
/// la que dos peticiones leen el mismo número y entregan el mismo folio. El SAT identifica
/// un comprobante por RFC + serie + folio, así que un folio repetido es un comprobante
/// rechazado —o peor, dos comprobantes válidos indistinguibles—. La reserva vive en el
/// procedimiento <c>dbo.ReservarFolio</c>, que hace la lectura y el aumento en un solo
/// <c>UPDATE ... WITH (UPDLOCK)</c> dentro de una transacción corta (ARQUITECTURA.md §5).
///
/// <para><b>La empresa no viaja desde el cliente</b></para>
/// Se le pasa al procedimiento desde el claim del token, y el procedimiento la exige en su
/// <c>WHERE</c>: pedir folio de la serie de otra empresa no devuelve nada, falla.
/// </summary>
public sealed class ServicioDeFolios(
    AppDbContext baseDeDatos,
    IContextoEmpresaInterno contexto,
    IServicioDeBitacora bitacora) : IServicioFolios
{
    public async Task<FolioReservadoDto> ReservarAsync(Guid serieId, CancellationToken ct)
    {
        var reservaId = Guid.NewGuid();
        var empresaId = contexto.EmpresaId;

        var parametros = new[]
        {
            new SqlParameter("@SerieId", serieId),
            new SqlParameter("@EmpresaId", empresaId),
            new SqlParameter("@ReservaId", reservaId),
            new SqlParameter("@MomentoUtc", DateTime.UtcNow)
        };

        var reserva = await baseDeDatos.Database
            .SqlQueryRaw<FolioReservadoDto>(
                "EXEC dbo.ReservarFolio @SerieId, @EmpresaId, @ReservaId, @MomentoUtc", parametros)
            .ToListAsync(ct);

        var folio = reserva.SingleOrDefault()
            ?? throw new InvalidOperationException(
                "El procedimiento de reserva no devolvió folio. Revisa que la serie exista y esté activa.");

        // Fuera de la transacción del procedimiento a propósito: la bitácora no debe alargar
        // el bloqueo del renglón de la serie, que es lo que serializa a todos los que piden
        // folio al mismo tiempo.
        bitacora.Registrar(
            EntidadesDeBitacora.ReservaFolio, reservaId.ToString(), AccionesDeBitacora.FolioReservado,
            despues: new { folio.Serie, folio.Folio, serieId });

        await baseDeDatos.SaveChangesAsync(ct);

        return folio;
    }

    public async Task ConfirmarAsync(Guid reservaId, Guid comprobanteId, CancellationToken ct)
    {
        var reserva = await CargarReservaAsync(reservaId, ct);

        if (reserva.Estado != EstadosDeReservaFolio.Reservado)
            throw new InvalidOperationException(
                $"La reserva {reservaId} ya está en estado '{reserva.Estado}' y no se puede confirmar.");

        reserva.Estado = EstadosDeReservaFolio.Confirmado;
        reserva.ComprobanteId = comprobanteId;
        reserva.MomentoResolucionUtc = DateTime.UtcNow;

        bitacora.Registrar(
            EntidadesDeBitacora.ReservaFolio, reservaId.ToString(), AccionesDeBitacora.FolioConfirmado,
            despues: new { reserva.Folio, comprobanteId });

        await baseDeDatos.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Marca la reserva como abandonada. <b>El folio no vuelve a la serie</b>: ARQUITECTURA.md §5
    /// exige que un folio tomado quede apartado aunque el timbrado falle, para que dos
    /// comprobantes distintos nunca puedan llevar el mismo número. Lo que deja este método
    /// es el registro que justifica el hueco.
    /// </summary>
    public async Task LiberarSiNoUsadoAsync(Guid reservaId, CancellationToken ct)
    {
        var reserva = await CargarReservaAsync(reservaId, ct);

        // Confirmar y luego abandonar sería un error de la mitad B, pero abandonar dos veces
        // puede pasar en un reintento: no vale la pena tratarlo como falla.
        if (reserva.Estado != EstadosDeReservaFolio.Reservado) return;

        reserva.Estado = EstadosDeReservaFolio.Abandonado;
        reserva.MomentoResolucionUtc = DateTime.UtcNow;

        bitacora.Registrar(
            EntidadesDeBitacora.ReservaFolio, reservaId.ToString(), AccionesDeBitacora.FolioAbandonado,
            antes: new { reserva.Folio, reserva.Estado },
            despues: new { reserva.Folio, Estado = EstadosDeReservaFolio.Abandonado });

        await baseDeDatos.SaveChangesAsync(ct);
    }

    private async Task<ReservaFolio> CargarReservaAsync(Guid reservaId, CancellationToken ct)
        => await baseDeDatos.ReservasFolio.FirstOrDefaultAsync(r => r.Id == reservaId, ct)
           ?? throw new InvalidOperationException($"No existe la reserva de folio {reservaId}.");
}
