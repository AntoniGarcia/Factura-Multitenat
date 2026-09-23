using System.Globalization;
using System.Text;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;

namespace Facturacion.Server.Modules.Plataforma.Catalogos;

public static class CargaDeCatalogosComercioExteriorCli
{
    public static async Task EjecutarAsync(WebApplication aplicacion, string catalogo, string ruta)
    {
        if (!File.Exists(ruta))
        {
            Console.Error.WriteLine($"No se encontró el archivo '{ruta}'.");
            Environment.ExitCode = 1;
            return;
        }

        using var alcance = aplicacion.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
        try
        {
            var total = catalogo switch
            {
                "c_INCOTERM" => await ImportarAsync(db, db.SatIncoterms, catalogo, ruta),
                "c_UnidadAduana" => await ImportarAsync(db, db.SatUnidadesAduana, catalogo, ruta),
                "c_FraccionArancelaria" => await ImportarAsync(db, db.SatFraccionesArancelarias, catalogo, ruta),
                _ => throw new InvalidOperationException("Catálogo no reconocido. Usa c_INCOTERM, c_UnidadAduana o c_FraccionArancelaria.")
            };
            Console.WriteLine($"{catalogo}: {total} claves vigentes.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Falló la carga de {catalogo}: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static async Task<int> ImportarAsync<T>(AppDbContext db, DbSet<T> tabla, string catalogo, string ruta)
        where T : class, ISatCatalogoSimple, new()
    {
        using var flujo = File.OpenRead(ruta);
        using var libro = WorkbookFactory.Create(flujo);
        var nombreArchivo = Normalizar(Path.GetFileNameWithoutExtension(ruta));
        if (!nombreArchivo.Contains(Normalizar(catalogo), StringComparison.Ordinal) ||
            (catalogo is "c_INCOTERM" or "c_UnidadAduana") && !nombreArchivo.Contains("20", StringComparison.Ordinal))
            throw new InvalidOperationException($"El nombre del archivo no corresponde a {catalogo}.");
        var hoja = Enumerable.Range(0, libro.NumberOfSheets)
            .Select(libro.GetSheetAt)
            .FirstOrDefault(h => Normalizar(h.SheetName).Contains(Normalizar(catalogo), StringComparison.Ordinal))
            ?? (libro.NumberOfSheets == 1 ? libro.GetSheetAt(0) : null)
            ?? throw new InvalidOperationException($"El libro no tiene una hoja identificable para {catalogo}.");

        var (filaCabecera, columnaClave, columnaDescripcion, columnaInicio, columnaFin) = BuscarCabecera(hoja, catalogo);
        var entradas = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        var formato = new DataFormatter(CultureInfo.InvariantCulture);
        for (var numero = filaCabecera + 1; numero <= hoja.LastRowNum; numero++)
        {
            var fila = hoja.GetRow(numero);
            var clave = Texto(fila, columnaClave, formato);
            if (clave is null) continue;
            var descripcion = Texto(fila, columnaDescripcion, formato);
            if (descripcion is null)
                throw new InvalidOperationException($"La clave {clave} no tiene descripción (fila {numero + 1}).");
            var maximoClave = catalogo == "c_FraccionArancelaria" ? 12 : 10;
            if (clave.Length > maximoClave || descripcion.Length > (catalogo == "c_FraccionArancelaria" ? 2000 : 500))
                throw new InvalidOperationException($"La fila {numero + 1} (clave {clave}) excede la longitud admitida: " +
                    $"clave {clave.Length}, descripción {descripcion.Length}.");
            var inicio = columnaInicio is { } indiceInicio ? Fecha(fila?.GetCell(indiceInicio), formato) : null;
            if (columnaInicio is { } inicioIndice && Texto(fila, inicioIndice, formato) is not null && inicio is null)
                throw new InvalidOperationException($"La fecha de inicio de la fila {numero + 1} no se pudo interpretar.");
            var fin = columnaFin is { } indice ? Fecha(fila?.GetCell(indice), formato) : null;
            if (columnaFin is { } finIndice && Texto(fila, finIndice, formato) is not null && fin is null)
                throw new InvalidOperationException($"La fecha de fin de la fila {numero + 1} no se pudo interpretar.");
            if (!entradas.TryAdd(clave, new T
            {
                Clave = clave,
                Descripcion = descripcion,
                FechaInicioVigencia = inicio ?? DateOnly.MinValue,
                FechaFinVigencia = fin,
                Vigente = (inicio is null || inicio <= DateOnly.FromDateTime(DateTime.UtcNow)) &&
                    (fin is null || fin >= DateOnly.FromDateTime(DateTime.UtcNow))
            }))
                throw new InvalidOperationException($"La clave {clave} está duplicada en el archivo.");
        }
        if (entradas.Count == 0) throw new InvalidOperationException("El archivo no contiene claves.");

        var existentes = await tabla.ToDictionaryAsync(x => x.Clave, StringComparer.OrdinalIgnoreCase);
        await using var transaccion = await db.Database.BeginTransactionAsync();
        foreach (var (clave, nuevo) in entradas)
        {
            if (!existentes.TryGetValue(clave, out var anterior))
            {
                tabla.Add(nuevo);
                continue;
            }
            anterior.Descripcion = nuevo.Descripcion;
            anterior.FechaInicioVigencia = nuevo.FechaInicioVigencia;
            anterior.FechaFinVigencia = nuevo.FechaFinVigencia;
            anterior.Vigente = nuevo.Vigente;
        }
        foreach (var (clave, anterior) in existentes)
            if (!entradas.ContainsKey(clave)) anterior.Vigente = false;

        var version = await db.CatalogoVersiones.FindAsync(catalogo);
        if (version is null)
        {
            version = new CatalogoVersion
            {
                Catalogo = catalogo,
                VersionCatalogo = "2.0",
                RevisionCatalogo = "archivo SAT"
            };
            db.CatalogoVersiones.Add(version);
        }
        version.VersionCatalogo = "2.0";
        version.RevisionCatalogo = "archivo SAT";
        version.FechaCargaUtc = DateTime.UtcNow;
        version.RenglonesVigentes = entradas.Values.Count(x => x.Vigente);
        await db.SaveChangesAsync();
        await transaccion.CommitAsync();
        return version.RenglonesVigentes;
    }

    private static (int Fila, int Clave, int Descripcion, int? Inicio, int? Fin) BuscarCabecera(ISheet hoja, string catalogo)
    {
        var formato = new DataFormatter(CultureInfo.InvariantCulture);
        for (var numero = 0; numero <= Math.Min(hoja.LastRowNum, 40); numero++)
        {
            var fila = hoja.GetRow(numero);
            if (fila is null || fila.LastCellNum <= 0) continue;
            var columnas = Enumerable.Range(0, fila.LastCellNum)
                .Select(i => (Indice: i, Titulo: Normalizar(Texto(fila, i, formato)))).ToList();
            var clave = columnas.FirstOrDefault(x => x.Titulo == Normalizar(catalogo) ||
                x.Titulo.StartsWith("clave", StringComparison.Ordinal) ||
                (catalogo == "c_UnidadAduana" && x.Titulo == "cunidadmedida") ||
                x.Titulo.StartsWith("fraccionarancelaria", StringComparison.Ordinal));
            var descripcion = columnas.FirstOrDefault(x => x.Titulo.StartsWith("descripcion", StringComparison.Ordinal) ||
                x.Titulo.StartsWith("nombre", StringComparison.Ordinal));
            if (clave.Titulo is null || descripcion.Titulo is null || clave.Indice == descripcion.Indice) continue;
            var inicio = columnas.FirstOrDefault(x => x.Titulo.Contains("fechainicio", StringComparison.Ordinal));
            var fin = columnas.FirstOrDefault(x => x.Titulo.Contains("fechafin", StringComparison.Ordinal));
            return (numero, clave.Indice, descripcion.Indice,
                inicio.Titulo is null ? null : inicio.Indice, fin.Titulo is null ? null : fin.Indice);
        }
        var primerasFilas = new List<string>();
        for (var numero = 0; numero <= Math.Min(hoja.LastRowNum, 7); numero++)
        {
            var fila = hoja.GetRow(numero);
            if (fila is null || fila.LastCellNum <= 0) continue;
            var celdas = new List<string>();
            for (var indice = 0; indice < Math.Min((int)fila.LastCellNum, 8); indice++)
                celdas.Add(Texto(fila, indice, formato) ?? "");
            if (celdas.Any(x => x.Length > 0)) primerasFilas.Add(string.Join(" | ", celdas));
        }
        throw new InvalidOperationException($"No se encontraron columnas de clave y descripción para {catalogo}. " +
            $"Primeras filas de '{hoja.SheetName}': {string.Join(" / ", primerasFilas)}");
    }

    private static string? Texto(IRow? fila, int columna, DataFormatter formato)
    {
        var celda = fila?.GetCell(columna);
        if (celda is null) return null;
        var texto = formato.FormatCellValue(celda).Trim();
        return texto.Length == 0 ? null : texto;
    }

    private static DateOnly? Fecha(ICell? celda, DataFormatter formato)
    {
        if (celda is null) return null;
        if (celda.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(celda) && celda.DateCellValue is { } fechaCelda)
            return DateOnly.FromDateTime(fechaCelda);
        var texto = formato.FormatCellValue(celda).Trim();
        return DateOnly.TryParse(texto, CultureInfo.GetCultureInfo("es-MX"), out var fecha) ? fecha : null;
    }

    private static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";
        return new string(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
            .ToArray()).ToLowerInvariant();
    }
}
