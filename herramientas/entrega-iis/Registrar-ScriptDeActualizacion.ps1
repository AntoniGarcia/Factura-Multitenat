param(
    [Parameter(Mandatory=$true)][string]$Archivo,
    [Parameter(Mandatory=$true)][string]$Nombre
)
$ErrorActionPreference = 'Stop'
$ruta = (Resolve-Path -LiteralPath $Archivo).Path
if ([IO.Path]::GetExtension($ruta) -ine '.sql' -or (Get-Item -LiteralPath $ruta).Length -gt 2MB) {
    throw 'Selecciona un SQL revisado de hasta 2 MB.'
}
$raiz = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$registro = Join-Path $raiz 'Facturacion.Server/Infra/ActualizacionBase/ScriptsPermitidos.json'
$huella = (Get-FileHash -LiteralPath $ruta -Algorithm SHA256).Hash
$entradas = @(Get-Content -LiteralPath $registro -Raw | ConvertFrom-Json | Where-Object { $_.Sha256 -ne $huella })
$entradas += [pscustomobject]@{ Nombre = $Nombre; Sha256 = $huella }
$json = ConvertTo-Json -InputObject $entradas -Depth 3
[IO.File]::WriteAllText($registro, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Write-Output 'Script registrado. Compila y publica la aplicación; el SQL se entrega por separado sin modificarlo.'
