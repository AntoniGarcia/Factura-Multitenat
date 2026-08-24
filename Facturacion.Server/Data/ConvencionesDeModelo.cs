using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Facturacion.Server.Data;

/// <summary>
/// Convenciones que aplican a todo el modelo, de hoy y de las fases que faltan. Están aquí
/// y no en cada configuración porque son reglas del dominio, no de una entidad: si se
/// dejaran a criterio de quien declara la columna, la primera que se olvide pasa inadvertida.
/// </summary>
public static class ConvencionesDeModelo
{
    public static void AplicarConvencionesDeFacturacion(this ModelConfigurationBuilder constructor)
    {
        // ARQUITECTURA.md §5: dinero con decimal(18,6). Sin esto, toda columna decimal nace con la
        // precisión por omisión de SQL Server y los centavos se pierden al redondear.
        constructor.Properties<decimal>().HavePrecision(18, 6);

        // ARQUITECTURA.md §5: las fechas se guardan en UTC.
        constructor.Properties<DateTime>().HaveConversion<FechaUtcConverter>();
    }
}

/// <summary>
/// Fuerza UTC en la escritura y marca el <see cref="DateTimeKind"/> en la lectura, para que
/// una fecha que sale de la base nunca se compare contra una hora local.
/// Una fecha sin <see cref="DateTimeKind"/> se toma como UTC, que es la convención del
/// sistema; solo se convierte lo que viene marcado como local.
/// </summary>
internal sealed class FechaUtcConverter() : ValueConverter<DateTime, DateTime>(
    fecha => fecha.Kind == DateTimeKind.Local ? fecha.ToUniversalTime() : fecha,
    fecha => DateTime.SpecifyKind(fecha, DateTimeKind.Utc));
