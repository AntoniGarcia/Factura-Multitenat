#Requires -Version 5.1
[CmdletBinding(SupportsShouldProcess=$true,ConfirmImpact='High')]
param([string]$Sitio,[switch]$Pausar)
$ErrorActionPreference='Stop'
$codigoSalida=0
$destinoIniciado=$false

function Ruta-LocalSegura([string]$Ruta) {
    $expandida=[Environment]::ExpandEnvironmentVariables($Ruta)
    if ($expandida -notmatch '^[A-Za-z]:\\') { throw 'Se requiere una carpeta de disco local, no una ruta de red.' }
    $normal=[IO.Path]::GetFullPath($expandida).TrimEnd('\')
    foreach ($prohibida in @([IO.Path]::GetPathRoot($normal),$env:USERPROFILE,$env:windir,$env:ProgramData,$env:ProgramFiles,${env:ProgramFiles(x86)})) {
        if ($prohibida -and $normal -eq $prohibida.TrimEnd('\')) { throw 'Selecciona una subcarpeta exclusiva para la aplicacion.' }
    }
    $actual=$normal
    while ($actual) {
        if ((Test-Path -LiteralPath $actual) -and ((Get-Item -LiteralPath $actual -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'No se admiten enlaces ni uniones en las rutas de instalacion.' }
        $actual=[IO.Path]::GetDirectoryName($actual)
    }
    return $normal
}

function Rutas-Solapadas([string]$Primera,[string]$Segunda) {
    return $Primera -eq $Segunda -or $Primera.StartsWith($Segunda+'\',[StringComparison]::OrdinalIgnoreCase) -or $Segunda.StartsWith($Primera+'\',[StringComparison]::OrdinalIgnoreCase)
}

try {
    $identidad=[Security.Principal.WindowsIdentity]::GetCurrent()
    if (-not ([Security.Principal.WindowsPrincipal]$identidad).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Cierra esta ventana. Haz clic derecho en INSTALAR.cmd y selecciona Ejecutar como administrador.'
    }
    Import-Module WebAdministration
    if (-not (Get-WebGlobalModule | Where-Object Name -eq 'AspNetCoreModuleV2')) { throw 'Falta el modulo de IIS. Instala o repara .NET 10 Hosting Bundle.' }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue) -or -not ((& dotnet --list-runtimes) -match '^Microsoft.AspNetCore.App 10\.')) { throw 'Falta ASP.NET Core Runtime 10 del Hosting Bundle.' }
    $origen=Ruta-LocalSegura (Join-Path $PSScriptRoot 'sitio')
    foreach ($archivo in @('PREPARAR-IIS.ps1','VERIFICAR.ps1')) {
        if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $archivo) -PathType Leaf)) { throw 'Extrae el paquete completo antes de instalar.' }
    }
    & (Join-Path $PSScriptRoot 'VERIFICAR.ps1') -DirectorioSitio $origen
    if (@(Get-ChildItem -LiteralPath $origen -Force -Recurse | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count -gt 0) { throw 'El paquete contiene enlaces o uniones.' }
    $sitios=@(Get-Website | Sort-Object Name)
    if ($sitios.Count -eq 0) { throw 'Primero crea el sitio en IIS con su carpeta y enlace HTTPS.' }
    if (-not $Sitio) {
        Write-Host ''
        Write-Host 'Selecciona el sitio IIS que recibira la aplicacion:'
        for ($i=0;$i -lt $sitios.Count;$i++) { Write-Host ('{0}. {1} | {2}' -f ($i+1),$sitios[$i].Name,$sitios[$i].PhysicalPath) }
        $seleccion=Read-Host 'Numero del sitio (Enter para cancelar)'
        if ([string]::IsNullOrWhiteSpace($seleccion)) { Write-Host 'Instalacion cancelada.'; return }
        $numero=0
        if (-not [int]::TryParse($seleccion,[ref]$numero) -or $numero -lt 1 -or $numero -gt $sitios.Count) { throw 'Selecciona un numero de la lista.' }
        $Sitio=[string]$sitios[$numero-1].Name
    }
    $elegidos=@($sitios | Where-Object Name -eq $Sitio)
    if ($elegidos.Count -ne 1) { throw 'No se encontro el sitio IIS indicado.' }
    $elegido=$elegidos[0]
    $destino=Ruta-LocalSegura $elegido.PhysicalPath
    $grupo=[string]$elegido.ApplicationPool
    if ($grupo -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$') { throw 'El grupo requiere un nombre simple de hasta 64 caracteres, sin espacios.' }
    if ($grupo -eq 'DefaultAppPool') { throw 'Asigna al sitio un grupo de aplicaciones exclusivo, no DefaultAppPool.' }
    if (-not (Test-Path -LiteralPath $destino -PathType Container)) { throw 'La carpeta fisica del sitio no existe.' }
    if (@(Get-ChildItem -LiteralPath $destino -Force).Count -gt 0) { throw 'La carpeta del sitio no esta vacia. Esta version instala solo en carpetas vacias; no actualiza ni reemplaza instalaciones.' }
    if (Rutas-Solapadas $origen $destino) { throw 'Extrae el paquete fuera de la carpeta de destino.' }
    if (@($sitios | Where-Object { $_.Name -ne $Sitio -and $_.ApplicationPool -eq $grupo }).Count -gt 0 -or @(Get-WebApplication | Where-Object ApplicationPool -eq $grupo).Count -gt 0) { throw 'El grupo esta compartido. Asigna uno exclusivo a este sitio.' }
    if ((Get-Item -LiteralPath "IIS:\AppPools\$grupo").processModel.identityType.ToString() -ne 'ApplicationPoolIdentity') { throw 'El grupo debe usar ApplicationPoolIdentity. No se cambiara su identidad.' }
    if (@(Get-WebApplication -Site $Sitio).Count -gt 0 -or @(Get-WebVirtualDirectory -Site $Sitio).Count -gt 0) { throw 'El sitio contiene aplicaciones o directorios virtuales; utiliza un sitio exclusivo.' }
    $privado=Ruta-LocalSegura (Join-Path $env:ProgramData ('FacturacionRevision\'+$grupo))
    if (Test-Path -LiteralPath $privado) { throw 'Ya existe la carpeta privada de este grupo. No se reemplazaran configuracion ni llaves.' }
    if (Rutas-Solapadas $destino $privado) { throw 'La carpeta privada debe quedar fuera del sitio.' }
    if (Rutas-Solapadas $PSScriptRoot $privado) { throw 'Extrae el instalador fuera de la carpeta privada.' }
    foreach ($otro in $sitios) {
        $rutaOtro=Ruta-LocalSegura $otro.PhysicalPath
        if (Rutas-Solapadas $PSScriptRoot $rutaOtro) { throw 'El paquete del instalador debe estar fuera de todas las carpetas de sitios IIS.' }
        if (Rutas-Solapadas $privado $rutaOtro) { throw 'La carpeta privada quedaria dentro de otro sitio IIS.' }
        if ($otro.Name -ne $Sitio -and (Rutas-Solapadas $destino $rutaOtro)) { throw 'La carpeta de destino se solapa con otro sitio IIS.' }
    }
    $enlaces=@($elegido.Bindings.Collection | Where-Object protocol -eq 'https')
    if ($enlaces.Count -eq 0) { throw 'Configura primero un enlace HTTPS y su certificado en IIS.' }
    $nombres=New-Object 'System.Collections.Generic.List[string]'
    $urls=New-Object 'System.Collections.Generic.List[string]'
    foreach ($enlace in $enlaces) {
        if ($enlace.bindingInformation -notmatch '^(.+):(\d+):([^:]*)$') { throw 'No se reconoce el enlace HTTPS del sitio.' }
        $direccion=$Matches[1]; $puerto=$Matches[2]; $hostEnlace=$Matches[3]
        if ([string]::IsNullOrWhiteSpace([string]$enlace.certificateHash)) { throw 'Falta asignar el certificado al enlace HTTPS.' }
        $huella=if ($enlace.certificateHash -is [byte[]]) { ([BitConverter]::ToString($enlace.certificateHash)).Replace('-','') } else { [string]$enlace.certificateHash }
        $almacen=[string]$enlace.certificateStoreName
        if (-not $almacen) { $almacen='My' }
        $cert=Get-Item -LiteralPath ("Cert:\LocalMachine\$almacen\$huella")
        if (-not $cert.HasPrivateKey -or $cert.NotAfter -lt (Get-Date) -or $cert.NotBefore -gt (Get-Date)) { throw 'El certificado HTTPS no tiene llave privada o esta fuera de vigencia.' }
        $nombreAcceso=if ($hostEnlace) { $hostEnlace } elseif ($direccion -in @('*','0.0.0.0','[::]','127.0.0.1','[::1]')) { 'localhost' } else { $direccion }
        if ([Uri]::CheckHostName($nombreAcceso.Trim('[',']')) -eq [UriHostNameType]::Unknown -or $nombreAcceso.Contains('*')) { throw 'El enlace necesita un nombre o direccion concretos, no un comodin.' }
        $nombres.Add($nombreAcceso)
        $sufijoPuerto=if ($puerto -eq '443') { '' } else { ':'+$puerto }
        $urls.Add(('https://'+$nombreAcceso+$sufijoPuerto+'/operador'))
    }
    Write-Host ''
    Write-Host ('Sitio: '+$Sitio)
    Write-Host ('Grupo: '+$grupo)
    Write-Host ('Publicacion: '+$destino)
    Write-Host ('Configuracion privada: '+(Join-Path $privado 'cnf'))
    Write-Host 'Se conservaran enlaces y certificados. No se conectara a SQL ni se ejecutaran scripts de base de datos.'
    if (-not $PSCmdlet.ShouldProcess("$Sitio | $destino",'Copiar publicacion nueva, preparar permisos y arrancar solo este sitio')) { return }
    # Se repiten las condiciones sensibles despues de la confirmacion para evitar sobrescribir cambios externos.
    $actual=Get-Website -Name $Sitio
    if ($actual.PhysicalPath -ne $elegido.PhysicalPath -or $actual.ApplicationPool -ne $grupo -or @(Get-ChildItem -LiteralPath $destino -Force).Count -gt 0 -or (Test-Path -LiteralPath $privado)) { throw 'El destino cambio durante la confirmacion; no se instalara.' }
    if ((Get-WebsiteState -Name $Sitio).Value -eq 'Started') { Stop-Website -Name $Sitio }
    if ((Get-WebAppPoolState -Name $grupo).Value -eq 'Started') { Stop-WebAppPool -Name $grupo }
    $destinoIniciado=$true
    Get-ChildItem -LiteralPath $origen -Force | Copy-Item -Destination $destino -Recurse
    # La copia nueva nunca debe heredar la ruta privada de ejemplo ni adoptar las llaves de otra instalacion.
    $archivoWeb=Join-Path $destino 'web.config'
    [xml]$web=Get-Content -LiteralPath $archivoWeb -Raw
    $nodos=@($web.configuration.location.'system.webServer'.aspNetCore.environmentVariables.environmentVariable | Where-Object name -eq 'Instalacion__Directorio')
    if ($nodos.Count -ne 1) { throw 'No hay una ruta privada unica en la publicacion.' }
    $nodos[0].SetAttribute('value',[string](Join-Path $privado 'cnf'))
    $web.Save($archivoWeb)
    & (Join-Path $PSScriptRoot 'PREPARAR-IIS.ps1') -Grupo $grupo -DirectorioSitio $destino -DirectorioPrivado $privado -NombreHost localhost -Confirm:$false
    [xml]$web=Get-Content -LiteralPath $archivoWeb -Raw
    $hosts=@($web.configuration.location.'system.webServer'.aspNetCore.environmentVariables.environmentVariable | Where-Object name -eq 'AllowedHosts')
    if ($hosts.Count -ne 1) { throw 'No hay una lista AllowedHosts unica.' }
    $hosts[0].SetAttribute('value',((@('localhost','127.0.0.1','[::1]')+@($nombres) | Select-Object -Unique) -join ';'))
    $web.Save($archivoWeb)
    & (Join-Path $PSScriptRoot 'VERIFICAR.ps1') -DirectorioSitio $destino
    Start-WebAppPool -Name $grupo
    Start-Website -Name $Sitio
    Write-Host ''
    Write-Host 'Instalacion preparada. Abre el asistente:'
    $urls | Select-Object -Unique | ForEach-Object { Write-Host $_ }
    Write-Host 'Captura en el navegador los parametros de la base existente. El asistente no instala el esquema SQL.'
    Write-Host 'Esta entrega de revision no pide clave de instalacion. Completa el asistente antes de compartir el acceso.'
    Write-Host 'Comprueba que el navegador confie en el certificado y que corresponda a la direccion. No ignores avisos HTTPS.'
} catch {
    $codigoSalida=1
    Write-Host ('No se completo la instalacion: '+$_.Exception.Message) -ForegroundColor Red
    if ($destinoIniciado) {
        Write-Host 'Conserva los archivos y las llaves. No repitas la instalacion ni borres carpetas para reparar el error.'
        Write-Host 'El sitio puede haber quedado detenido. Revisa el mensaje antes de iniciarlo.'
    }
} finally {
    if ($Pausar) { [void](Read-Host 'Presiona Enter para cerrar') }
}
exit $codigoSalida
