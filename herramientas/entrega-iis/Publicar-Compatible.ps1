param(
    [Parameter(Mandatory=$true)][string]$Salida,
    [Parameter(Mandatory=$true)][string]$NombreHost
)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if ($NombreHost -notmatch '^[A-Za-z0-9.-]+$') { throw 'Escribe solo el hostname, sin protocolo ni puerto.' }
$destino = [IO.Path]::GetFullPath($Salida)
$sitio = Join-Path $destino 'sitio'
dotnet publish (Join-Path $raiz 'Facturacion.Server/Facturacion.Server.csproj') -c Release -p:PublicacionIisAutocontenida=true -o $sitio
if ($LASTEXITCODE -ne 0) { throw 'No se pudo publicar la aplicación.' }
$runtime = Get-Content -LiteralPath (Join-Path $sitio 'Facturacion.Server.runtimeconfig.json') -Raw | ConvertFrom-Json
if (-not $runtime.runtimeOptions.includedFrameworks) { throw 'La publicación no incluye el runtime.' }
foreach ($archivo in @('Facturacion.Server.exe','coreclr.dll','hostfxr.dll','hostpolicy.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $sitio $archivo))) { throw "Falta $archivo" }
}
[xml]$config = Get-Content -LiteralPath (Join-Path $raiz 'herramientas/entrega-archivos/web.config')
$asp = $config.configuration.location.'system.webServer'.aspNetCore
$asp.SetAttribute('processPath', '.\Facturacion.Server.exe')
$asp.SetAttribute('arguments', '')
$asp.SetAttribute('hostingModel', 'outofprocess')
($asp.environmentVariables.environmentVariable | Where-Object name -eq 'AllowedHosts').SetAttribute('value', "localhost;127.0.0.1;[::1];$NombreHost")
$config.Save((Join-Path $sitio 'web.config'))
$scriptSql = Join-Path $raiz 'docs/sql/Facturacion.Actualizar-Completo-Sql2014.sql'
if (Test-Path -LiteralPath $scriptSql -PathType Leaf) {
    Copy-Item -LiteralPath $scriptSql -Destination (Join-Path $destino 'Actualizar-base.sql')
} else {
    Write-Output 'Entrega el script SQL por separado si la base necesita actualizarse.'
}
Write-Output "Publicación Windows x64 con runtime incluido: $destino"
