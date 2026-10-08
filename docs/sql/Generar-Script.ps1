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
[IO.File]::WriteAllText($rutaSql, $contenidoSql, [Text.UTF8Encoding]::new($false))
Write-Output "Script generado: $rutaSql"
