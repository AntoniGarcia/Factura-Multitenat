param([Parameter(Mandatory=$true)][string]$Privado,[Parameter(Mandatory=$true)][string]$Salida,[switch]$Reiniciar)
$ErrorActionPreference = 'Stop'
$resultado = [ordered]@{ Error=$null }
try {
    Import-Module WebAdministration
    $nombre = 'FacturacionRevisionLocal20261020'
    $binding = Get-WebBinding -Name $nombre -Protocol https | Select-Object -First 1
    if (-not (Get-WebBinding -Name $nombre -Protocol https | Where-Object bindingInformation -eq '[::1]:7443:')) {
        New-WebBinding -Name $nombre -Protocol https -IPAddress '[::1]' -Port 7443
        $nuevo = Get-WebBinding -Name $nombre -Protocol https | Where-Object bindingInformation -eq '[::1]:7443:'
        $nuevo.AddSslCertificate($binding.certificateHash, 'My')
    }
    $archivo = Join-Path $Privado 'cnf\appsettings.ArranqueRevision.json'
    $resultado.ArranqueExiste = Test-Path -LiteralPath $archivo
    $resultado.ClaveExiste = Test-Path -LiteralPath (Join-Path $Privado 'cnf\clave-instalacion.txt')
    $resultado.HashAntes = if ($resultado.ArranqueExiste) { (Get-FileHash -LiteralPath $archivo).Hash } else { $null }
    if ($Reiniciar) { Restart-WebAppPool -Name $nombre }
    $resultado.Bindings = @(Get-WebBinding -Name $nombre | Select-Object protocol,bindingInformation)
    $resultado.HashDespues = if (Test-Path -LiteralPath $archivo) { (Get-FileHash -LiteralPath $archivo).Hash } else { $null }
    $resultado.EstadoGrupo = (Get-WebAppPoolState -Name $nombre).Value
} catch { $resultado.Error = $_.Exception.Message }
[IO.File]::WriteAllText($Salida, ($resultado | ConvertTo-Json -Depth 4))
if ($resultado.Error) { exit 1 }
