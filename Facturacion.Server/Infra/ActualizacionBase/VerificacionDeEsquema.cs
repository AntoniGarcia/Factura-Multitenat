using Microsoft.EntityFrameworkCore;
using Facturacion.Server.Data;

namespace Facturacion.Server.Infra.ActualizacionBase;

public static class VerificacionDeEsquema
{
    public static async Task<bool> CoincideAsync(AppDbContext db, CancellationToken ct)
    {
        var modelo = db.Model.GetRelationalModel();
        var columnas = new Dictionary<(string Tabla, string Columna), (string Tipo, bool Nulo)>();
        var indices = new Dictionary<(string Tabla, string Indice), (bool Unico, List<string> Columnas)>();
        var llaves = new HashSet<(string Tabla, string Llave)>();
        var conexion = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = """
                SELECT t.name,c.name,ty.name,c.max_length,c.precision,c.scale,c.is_nullable
                FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
                JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
                WHERE s.name=N'dbo' AND t.is_ms_shipped=0;
                SELECT t.name,i.name,i.is_unique,c.name FROM sys.tables t
                JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.indexes i ON i.object_id=t.object_id
                JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
                JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                WHERE s.name=N'dbo' AND ic.key_ordinal>0 AND i.is_disabled=0 AND i.is_hypothetical=0 ORDER BY t.name,i.name,ic.key_ordinal;
                SELECT t.name,f.name FROM sys.foreign_keys f JOIN sys.tables t ON t.object_id=f.parent_object_id
                JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'dbo' AND f.is_disabled=0 AND f.is_not_trusted=0;
                """;
            await using var lector = await comando.ExecuteReaderAsync(ct);
            while (await lector.ReadAsync(ct))
            {
                var tipo = lector.GetString(2);
                var longitud = lector.GetInt16(3);
                var forma = tipo switch
                {
                    "nvarchar" or "nchar" => tipo + "(" + (longitud < 0 ? "max" : (longitud / 2).ToString()) + ")",
                    "varchar" or "char" or "varbinary" or "binary" => tipo + "(" + (longitud < 0 ? "max" : longitud.ToString()) + ")",
                    "decimal" or "numeric" => $"decimal({lector.GetByte(4)},{lector.GetByte(5)})",
                    "datetime2" or "datetimeoffset" or "time" => $"{tipo}({lector.GetByte(5)})",
                    "timestamp" => "rowversion",
                    _ => tipo
                };
                columnas[(lector.GetString(0), lector.GetString(1))] = (NormalizarTipo(forma), lector.GetBoolean(6));
            }
            await lector.NextResultAsync(ct);
            while (await lector.ReadAsync(ct))
            {
                var clave = (lector.GetString(0), lector.GetString(1));
                if (!indices.ContainsKey(clave)) indices[clave] = (lector.GetBoolean(2), []);
                indices[clave].Columnas.Add(lector.GetString(3));
            }
            await lector.NextResultAsync(ct);
            while (await lector.ReadAsync(ct)) llaves.Add((lector.GetString(0), lector.GetString(1)));
        }
        finally { await db.Database.CloseConnectionAsync(); }
        foreach (var tabla in modelo.Tables)
        {
            foreach (var columna in tabla.Columns)
                if (!columnas.TryGetValue((tabla.Name, columna.Name), out var actual)
                    || actual.Tipo != NormalizarTipo(columna.StoreType) || actual.Nulo != columna.IsNullable) return false;
            if (tabla.PrimaryKey is { } primaria && (!indices.TryGetValue((tabla.Name, primaria.Name), out var pk)
                || !pk.Unico || !pk.Columnas.SequenceEqual(primaria.Columns.Select(x => x.Name)))) return false;
            foreach (var indice in tabla.Indexes)
                if (!indices.TryGetValue((tabla.Name, indice.Name), out var actual)
                    || actual.Unico != indice.IsUnique || !actual.Columnas.SequenceEqual(indice.Columns.Select(x => x.Name))) return false;
            foreach (var llave in tabla.ForeignKeyConstraints)
                if (!llaves.Contains((tabla.Name, llave.Name))) return false;
        }
        return true;
    }

    public static string NormalizarTipo(string tipo) => tipo.Replace(" ", "").ToLowerInvariant()
        .Replace("numeric(", "decimal(").Replace("timestamp", "rowversion")
        .Replace("datetime2(7)", "datetime2").Replace("datetimeoffset(7)", "datetimeoffset").Replace("time(7)", "time");
}
