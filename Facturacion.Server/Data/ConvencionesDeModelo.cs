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
        // Usa decimal(18,6) para conservar la precisión de los importes.
        constructor.Properties<decimal>().HavePrecision(18, 6);

        // Las fechas se guardan en UTC.
        constructor.Properties<DateTime>().HaveConversion<FechaUtcConverter>();

        // Las claves Guid las asigna el código; ver ClavesAsignadasPorElCodigo.
        constructor.Conventions.Add(_ => new ClavesAsignadasPorElCodigo());
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
