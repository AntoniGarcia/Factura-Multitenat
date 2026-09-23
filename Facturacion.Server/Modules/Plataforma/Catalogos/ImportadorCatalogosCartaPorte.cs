using System.Globalization;
using System.Text;
using Facturacion.Server.Data;
using Facturacion.Server.Data.Entidades.Plataforma.Catalogos;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;

namespace Facturacion.Server.Modules.Plataforma.Catalogos;

/// <summary>
/// Sincroniza los catálogos de Carta Porte 3.1 desde el libro oficial del SAT. No hay valores
/// sembrados: si el archivo cambia de forma o no trae una hoja requerida, la carga falla antes
/// de escribir para que ninguna clave fiscal quede incompleta o inventada.
/// </summary>
public sealed class ImportadorCatalogosCartaPorte(AppDbContext baseDeDatos)
{
    private const string RevisionDelArchivo = "archivo SAT";

    private static readonly string[] CatalogosDisponibles =
        ["c_ConfigAutotransporte", "c_TipoPermiso", "c_FiguraTransporte", "c_ClaveProdServCP"];

    public async Task<IReadOnlyList<string>> ImportarAsync(
        string rutaArchivo, CancellationToken ct, string? soloEsteCatalogo = null)
    {
        using var flujo = File.OpenRead(rutaArchivo);
        using var libro = WorkbookFactory.Create(flujo);

        var catalogos = new (string Nombre, Func<Task<string>> Importar)[]
        {
            ("c_ConfigAutotransporte", () => ImportarSimpleAsync(
                libro, "c_ConfigAutotransporte", baseDeDatos.SatConfiguracionesAutotransporte, ct)),
            ("c_TipoPermiso", () => ImportarSimpleAsync(
                libro, "c_TipoPermiso", baseDeDatos.SatTiposPermiso, ct)),
            ("c_FiguraTransporte", () => ImportarSimpleAsync(
                libro, "c_FiguraTransporte", baseDeDatos.SatFigurasTransporte, ct)),
            ("c_ClaveProdServCP", () => ImportarSimpleAsync(
                libro, "c_ClaveProdServCP", baseDeDatos.SatClavesProdServCartaPorte, ct))
        };

        var seleccionados = soloEsteCatalogo is null
            ? catalogos
            : [.. catalogos.Where(c => string.Equals(c.Nombre, soloEsteCatalogo, StringComparison.OrdinalIgnoreCase))];

        if (seleccionados.Length == 0)
            throw new InvalidOperationException(
                $"'{soloEsteCatalogo}' no es un catálogo de Carta Porte conocido. Los válidos son: " +
                string.Join(", ", CatalogosDisponibles));

        var resumen = new List<string>(seleccionados.Length);

        foreach (var catalogo in seleccionados)
            resumen.Add(await catalogo.Importar());

        return resumen;
    }

    private async Task<string> ImportarSimpleAsync<TEntidad>(
        IWorkbook libro, string nombre, DbSet<TEntidad> tabla, CancellationToken ct)
        where TEntidad : class, ISatCatalogoSimple, new()
    {
        var hoja = libro.GetSheet(nombre)
            ?? throw new InvalidOperationException($"El archivo no tiene la hoja '{nombre}'.");
        var encabezado = EncabezadoDe(hoja);
        var nuevos = new Dictionary<string, TEntidad>(StringComparer.OrdinalIgnoreCase);

        for (var renglon = encabezado.Fila + 1; renglon <= hoja.LastRowNum; renglon++)
        {
            var fila = hoja.GetRow(renglon);
            var clave = Texto(fila, encabezado.Clave);
            if (clave is null) continue;

            var fin = encabezado.Fin is { } columnaFin ? Fecha(fila, columnaFin) : null;
            nuevos[clave] = new TEntidad
            {
                Clave = clave,
                Descripcion = Texto(fila, encabezado.Descripcion) ?? clave,
                FechaInicioVigencia = encabezado.Inicio is { } columnaInicio
                    ? Fecha(fila, columnaInicio) ?? DateOnly.MinValue
                    : DateOnly.MinValue,
                FechaFinVigencia = fin,
                Vigente = fin is null
            };
        }

        if (nuevos.Count == 0)
            throw new InvalidOperationException($"La hoja '{nombre}' no contiene claves para importar.");

        var existentes = await tabla.ToDictionaryAsync(x => x.Clave, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var (clave, nuevo) in nuevos)
        {
            if (!existentes.TryGetValue(clave, out var actual))
            {
                tabla.Add(nuevo);
                continue;
            }

            actual.Descripcion = nuevo.Descripcion;
            actual.FechaInicioVigencia = nuevo.FechaInicioVigencia;
            actual.FechaFinVigencia = nuevo.FechaFinVigencia;
            actual.Vigente = nuevo.Vigente;
        }

        foreach (var (clave, existente) in existentes)
        {
            if (nuevos.ContainsKey(clave)) continue;

            existente.Vigente = false;
            existente.FechaFinVigencia ??= DateOnly.FromDateTime(DateTime.UtcNow);
        }

        var vigentes = nuevos.Values.Count(x => x.Vigente);
        await ActualizarVersionAsync(nombre, vigentes, ct);
        await baseDeDatos.SaveChangesAsync(ct);

        return $"{nombre}: {vigentes} vigentes";
    }

    private async Task ActualizarVersionAsync(string catalogo, int vigentes, CancellationToken ct)
    {
        var registro = await baseDeDatos.CatalogoVersiones.FindAsync([catalogo], ct);

        if (registro is null)
        {
            registro = new CatalogoVersion
            {
                Catalogo = catalogo,
                VersionCatalogo = "3.1",
                RevisionCatalogo = RevisionDelArchivo
            };
            baseDeDatos.CatalogoVersiones.Add(registro);
        }

        registro.VersionCatalogo = "3.1";
        registro.RevisionCatalogo = RevisionDelArchivo;
        registro.FechaCargaUtc = DateTime.UtcNow;
        registro.RenglonesVigentes = vigentes;
    }

    /// <summary>
    /// El libro oficial de Carta Porte no conserva el mismo texto de cabecera entre todas sus
    /// publicaciones: algunas versiones nombran la primera columna con la clave del catálogo
    /// y otras simplemente «Clave». Se detectan las columnas semánticamente para que una
    /// actualización del SAT no convierta una carga válida en un supuesto formato fijo.
    /// </summary>
    private static Encabezado EncabezadoDe(ISheet hoja)
    {
        var limite = hoja.LastRowNum;
        for (var renglon = 0; renglon <= limite; renglon++)
        {
            var fila = hoja.GetRow(renglon);
            if (fila is null || fila.LastCellNum <= 0) continue;

            var columnas = Enumerable.Range(0, fila.LastCellNum)
                .Select(columna => (Columna: columna, Texto: Normalizar(Texto(fila, columna))))
                .ToList();

            var clave = columnas.FirstOrDefault(x => EsEncabezadoDeClave(x.Texto)).Columna;
            var descripcion = columnas.FirstOrDefault(x => EsEncabezadoDeDescripcion(x.Texto)).Columna;

            // FirstOrDefault devuelve cero también cuando no hay coincidencia; se comprueba
            // explícitamente con Any para no confundir una cabecera real en la columna cero.
            var tieneClave = columnas.Any(x => x.Columna == clave && EsEncabezadoDeClave(x.Texto));
            var tieneDescripcion = columnas.Any(x => x.Columna == descripcion && EsEncabezadoDeDescripcion(x.Texto));

            if (!tieneClave || !tieneDescripcion) continue;

            var inicio = columnas.FirstOrDefault(x => x.Texto.Contains("fechainicio", StringComparison.Ordinal)).Columna;
            var fin = columnas.FirstOrDefault(x => x.Texto.Contains("fechafin", StringComparison.Ordinal)).Columna;

            return new Encabezado(
                renglon,
                clave,
                descripcion,
                columnas.Any(x => x.Columna == inicio && x.Texto.Contains("fechainicio", StringComparison.Ordinal)) ? inicio : null,
                columnas.Any(x => x.Columna == fin && x.Texto.Contains("fechafin", StringComparison.Ordinal)) ? fin : null);
        }

        throw new InvalidOperationException(
            $"No se encontraron las columnas Clave y Descripción en las primeras filas de '{hoja.SheetName}'.");
    }

    private static bool EsEncabezadoDeClave(string texto) =>
        texto.StartsWith("clave", StringComparison.Ordinal) ||
        texto is "cconfigautotransporte" or "ctipopermiso" or "cfiguratransporte" or "cclaveprodservcp";

    private static bool EsEncabezadoDeDescripcion(string texto) =>
        texto.StartsWith("descripcion", StringComparison.Ordinal) ||
        texto.StartsWith("nombre", StringComparison.Ordinal);

    private static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        return new string(descompuesto
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
            .ToArray())
            .ToLowerInvariant();
    }

    private sealed record Encabezado(int Fila, int Clave, int Descripcion, int? Inicio, int? Fin);

    private static string? Texto(IRow? fila, int columna)
    {
        var celda = fila?.GetCell(columna);
        if (celda is null) return null;

        var tipo = celda.CellType == CellType.Formula ? celda.CachedFormulaResultType : celda.CellType;
        if (tipo == CellType.Blank) return null;

        var valor = tipo == CellType.Numeric
            ? celda.NumericCellValue.ToString(CultureInfo.InvariantCulture)
            : tipo == CellType.String ? celda.StringCellValue : celda.ToString();

        valor = valor?.Trim();
        return string.IsNullOrEmpty(valor) ? null : valor;
    }

    private static DateOnly? Fecha(IRow? fila, int columna)
    {
        var celda = fila?.GetCell(columna);
        if (celda is null || celda.CellType != CellType.Numeric || !DateUtil.IsCellDateFormatted(celda))
            return null;

        return celda.DateCellValue is { } fecha ? DateOnly.FromDateTime(fecha) : null;
    }
}
