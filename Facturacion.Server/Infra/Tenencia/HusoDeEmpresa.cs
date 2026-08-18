using Facturacion.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Infra.Tenencia;

/// <summary>
/// Responde «qué día y qué mes es» para la empresa activa.
///
/// <para><b>Por qué esto existe en vez de usar <c>DateTime.UtcNow</c> y ya</b></para>
/// La base guarda todo en UTC y se muestra en el huso del lugar de expedición
/// (CLAUDE.md §5). México va seis horas detrás, así que entre las 18:00 y la medianoche
/// locales el «hoy» de UTC ya es mañana. Un corte mensual hecho con la fecha UTC mete las
/// facturas de la tarde del día 31 en el mes siguiente, y el reporte no cuadra contra la
/// contabilidad del contador — que es el único cuadre que importa.
///
/// <para>
/// Vive aquí, y no en el tablero ni en el resumen de documentos, porque los dos lo
/// necesitan. Repetir la conversión en cada uno es cómo se separan dos respuestas que
/// deberían ser la misma.
/// </para>
/// </summary>
public sealed class HusoDeEmpresa(AppDbContext baseDeDatos, IContextoEmpresaInterno contexto)
{
    private TimeZoneInfo? _huso;

    /// <summary>Huso del lugar de expedición. UTC si la empresa no tiene uno utilizable.</summary>
    public async Task<TimeZoneInfo> ObtenerAsync(CancellationToken ct)
    {
        if (_huso is not null) return _huso;

        var zona = contexto.EmpresaActual is { } empresaId
            ? await baseDeDatos.Empresas
                .AsNoTracking()
                .Where(e => e.Id == empresaId)
                .Select(e => e.ZonaHoraria)
                .FirstOrDefaultAsync(ct)
            : null;

        return _huso = Resolver(zona);
    }

    /// <summary>Fecha de calendario de hoy para la empresa.</summary>
    public async Task<DateOnly> HoyAsync(CancellationToken ct)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, await ObtenerAsync(ct)));

    /// <summary>Primer y último día del mes en curso para la empresa, ambos incluidos.</summary>
    public async Task<(DateOnly Desde, DateOnly Hasta)> MesEnCursoAsync(CancellationToken ct)
    {
        var hoy = await HoyAsync(ct);
        var primero = new DateOnly(hoy.Year, hoy.Month, 1);

        return (primero, primero.AddMonths(1).AddDays(-1));
    }

    /// <summary>
    /// Convierte un rango de fechas de calendario al intervalo UTC que se consulta. El
    /// extremo final llega al último instante del día: con la medianoche se perdería todo lo
    /// emitido ese día.
    /// </summary>
    public async Task<(DateTime Inicio, DateTime Fin)> ARangoUtcAsync(
        DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var huso = await ObtenerAsync(ct);

        return (
            TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(desde.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), huso),
            TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(hasta.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Unspecified), huso));
    }

    // Una zona inválida no puede tumbar una pantalla: el alta de empresa ya la valida, y si
    // aun así llegara una mala, UTC da un corte razonable en vez de una excepción.
    private static TimeZoneInfo Resolver(string? zona)
    {
        if (string.IsNullOrWhiteSpace(zona)) return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(zona);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
