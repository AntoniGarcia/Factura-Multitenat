param([string]$Salida)
$ErrorActionPreference = 'Stop'
$resultado = [ordered]@{ Fecha = [DateTimeOffset]::Now.ToString('o'); Administrador = $false; Servicios = @(); Modulos = @(); Sitios = @(); Error = $null }
try {
    $identidad = [Security.Principal.WindowsIdentity]::GetCurrent()
    $resultado.Administrador = ([Security.Principal.WindowsPrincipal]$identidad).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    $resultado.Servicios = @(Get-Service W3SVC,WAS | Select-Object Name,@{Name='Estado';Expression={$_.Status.ToString()}})
    Import-Module WebAdministration
    $resultado.Modulos = @(Get-WebGlobalModule | Where-Object Name -Like '*AspNetCore*' | Select-Object Name,Image)
    $resultado.Sitios = @(Get-Website | Select-Object Name,State,PhysicalPath,@{Name='Bindings';Expression={@($_.Bindings.Collection | ForEach-Object { $_.protocol + ' ' + $_.bindingInformation })}})
} catch { $resultado.Error = $_.Exception.Message }
$json = $resultado | ConvertTo-Json -Depth 5
if ($Salida) { [IO.File]::WriteAllText($Salida, $json) } else { $json }
if ($resultado.Error) { exit 1 }
