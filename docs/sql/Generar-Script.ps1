param()

$ErrorActionPreference = 'Stop'
$raizProyecto = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$rutaSql = Join-Path $PSScriptRoot 'Facturacion.Instalacion.sql'
Push-Location (Join-Path $raizProyecto 'Facturacion.Server')
try {
    dotnet ef migrations script 0 --idempotent --configuration Release --no-build --project .\Facturacion.Server.csproj --startup-project .\Facturacion.Server.csproj --output $rutaSql
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo generar el script de migraciones.' }
}
finally { Pop-Location }

# SQL Server exige que cada definición de procedimiento sea el primer comando de su lote.
# EF envuelve el SQL manual en IF; EXEC conserva la condición sin romper esa exigencia.
$contenidoSql = [IO.File]::ReadAllText($rutaSql)
$patronProcedimiento = '(?ms)^    CREATE OR ALTER PROCEDURE\b.*?(?=^END;)'
$contenidoSql = [regex]::Replace($contenidoSql, $patronProcedimiento, {
    param($coincidencia)
    $definicion = $coincidencia.Value.TrimEnd().Replace("'", "''")
    "    EXEC(N'" + $definicion.TrimStart() + "');`r`n"
})
# Compilar cada paso solo cuando le corresponda evita referencias a columnas futuras.
$patronPaso = '(?ms)(^IF NOT EXISTS \(\r?\n    SELECT \* FROM \[__EFMigrationsHistory\].*?^BEGIN\r?\n)(.*?)(^END;)'
$contenidoSql = [regex]::Replace($contenidoSql, $patronPaso, {
    param($paso)
    $cuerpo = $paso.Groups[2].Value.TrimEnd().Replace("'", "''")
    $paso.Groups[1].Value + "    EXEC(N'" + $cuerpo.TrimStart() + "');`r`n" + $paso.Groups[3].Value
})
$opcionesSql = "SET QUOTED_IDENTIFIER ON;`r`nSET ANSI_NULLS ON;`r`nSET ANSI_PADDING ON;`r`nSET ANSI_WARNINGS ON;`r`nSET ARITHABORT ON;`r`nSET CONCAT_NULL_YIELDS_NULL ON;`r`nSET NUMERIC_ROUNDABORT OFF;`r`nGO`r`n"
[IO.File]::WriteAllText($rutaSql, $opcionesSql + $contenidoSql, [Text.UTF8Encoding]::new($false))
Write-Output "Script generado: $rutaSql"
