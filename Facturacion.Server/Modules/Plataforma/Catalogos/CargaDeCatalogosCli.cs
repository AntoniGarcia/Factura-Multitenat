using Facturacion.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Catalogos;

/// <summary>
/// Punto de entrada de <c>dotnet run -- --cargar-catalogos ruta.xls</c>. Vive aquí y no en
/// <c>Program.cs</c> (REPARTO-EQUIPO.md §5: ese archivo no crece) — Program.cs solo detecta
/// el modificador y llama a <see cref="EjecutarAsync"/>.
/// </summary>
public static class CargaDeCatalogosCli
{
    public static async Task EjecutarAsync(
        WebApplication aplicacion, string rutaArchivo, string? soloEsteCatalogo = null)
    {
        if (!File.Exists(rutaArchivo))
        {
            Console.Error.WriteLine($"No se encontró el archivo '{rutaArchivo}'.");
            Environment.ExitCode = 1;
            return;
        }

        using var alcance = aplicacion.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();

        // Los 30 segundos por omisión de EF Core están pensados para una petición web, no
        // para una carga de ~300.000 renglones con los índices de texto completo poblándose
        // en paralelo. Con el catálogo ya cargado la reimportación toca todo otra vez para
        // comparar, y ahí es donde se agotaba el plazo.
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));

        var importador = new ImportadorCatalogosSat(db);

        var inicio = DateTime.UtcNow;

        Console.WriteLine(soloEsteCatalogo is null
            ? $"Cargando todos los catálogos desde '{rutaArchivo}'…"
            : $"Cargando solo {soloEsteCatalogo} desde '{rutaArchivo}'…");

        IReadOnlyList<string> resumen;
        try
        {
            resumen = await importador.ImportarAsync(rutaArchivo, CancellationToken.None, soloEsteCatalogo);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Falló la carga: {ex.Message}");
            Environment.ExitCode = 1;
            return;
        }

        foreach (var linea in resumen)
            Console.WriteLine("  " + linea);

        Console.WriteLine($"Listo en {(DateTime.UtcNow - inicio).TotalSeconds:F1} s.");
    }
}
