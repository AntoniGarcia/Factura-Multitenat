using Facturacion.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Server.Modules.Plataforma.Catalogos;

/// <summary>Entrada de consola para el libro oficial de catálogos de Carta Porte 3.1.</summary>
public static class CargaDeCatalogosCartaPorteCli
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
        var baseDeDatos = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
        baseDeDatos.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

        try
        {
            var importador = new ImportadorCatalogosCartaPorte(baseDeDatos);
            var resumen = await importador.ImportarAsync(rutaArchivo, CancellationToken.None, soloEsteCatalogo);

            foreach (var linea in resumen)
                Console.WriteLine("  " + linea);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Falló la carga de Carta Porte: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }
}
