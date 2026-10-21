#Requires -Version 5.1
[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$')][string]$Grupo,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.-]*$')][string]$NombreHost = 'localhost',
    [string]$DirectorioSitio = (Join-Path $PSScriptRoot 'sitio'),
    [string]$DirectorioPrivado
)
$ErrorActionPreference = 'Stop'

function Ruta-Local([string]$Ruta) {
    if ($Ruta -notmatch '^[A-Za-z]:\\') { throw 'Utiliza una ruta absoluta de disco local.' }
    $normal = [IO.Path]::GetFullPath($Ruta).TrimEnd('\')
    if ($normal -eq [IO.Path]::GetPathRoot($normal).TrimEnd('\')) { throw 'No se permite usar la raiz de una unidad.' }
    $actual = $normal
    while ($actual) {
        if (Test-Path -LiteralPath $actual) {
            if ((Get-Item -LiteralPath $actual -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "No se admiten enlaces o uniones en la ruta: $actual" }
        }
        $padre = [IO.Path]::GetDirectoryName($actual)
        if ($padre -eq $actual) { break }
        $actual = $padre
    }
    return $normal
}

$identidadActual = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]$identidadActual).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Abre Windows PowerShell como administrador y ejecuta de nuevo el comando.'
}
Import-Module WebAdministration
if (-not (Get-WebGlobalModule | Where-Object Name -eq 'AspNetCoreModuleV2')) { throw 'Instala o repara .NET 10 Hosting Bundle: falta AspNetCoreModuleV2.' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'No se encontro dotnet. Comprueba Hosting Bundle y reinicia si acaba de instalarse.' }
if (-not ((& dotnet --list-runtimes) -match '^Microsoft.AspNetCore.App 10\.')) { throw 'Falta ASP.NET Core Runtime 10.' }
if ([Uri]::CheckHostName($NombreHost) -eq [UriHostNameType]::Unknown) { throw 'Introduce un nombre DNS o localhost, sin protocolo, puerto ni ruta.' }
$DirectorioSitio = Ruta-Local $DirectorioSitio
if (-not $DirectorioPrivado) { $DirectorioPrivado = Join-Path $env:ProgramData ('FacturacionRevision\' + $Grupo) }
$DirectorioPrivado = Ruta-Local $DirectorioPrivado
if ($DirectorioPrivado -eq $DirectorioSitio -or $DirectorioPrivado.StartsWith($DirectorioSitio+'\',[StringComparison]::OrdinalIgnoreCase) -or
    $DirectorioSitio.StartsWith($DirectorioPrivado+'\',[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Las carpetas publica y privada deben estar separadas; ninguna puede contener a la otra.'
}
foreach ($prohibida in @($env:USERPROFILE,$env:windir,$env:ProgramData,$env:ProgramFiles,${env:ProgramFiles(x86)})) {
    if ($prohibida -and $DirectorioPrivado -eq $prohibida.TrimEnd('\')) { throw 'Selecciona una subcarpeta exclusiva de esta aplicacion.' }
}
$archivoWeb = Join-Path $DirectorioSitio 'web.config'
foreach ($requerido in @($archivoWeb,(Join-Path $DirectorioSitio 'Facturacion.Server.dll'))) {
    if (-not (Test-Path -LiteralPath $requerido -PathType Leaf)) { throw "Falta $requerido. Extrae todo el ZIP." }
}
if ((Get-Item -LiteralPath $archivoWeb).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'web.config no puede ser un enlace.' }
[xml]$web = [IO.File]::ReadAllText($archivoWeb)
$asp = $web.configuration.location.'system.webServer'.aspNetCore
if (-not $asp) { throw 'web.config no contiene aspNetCore.' }
$rutaGrupo = 'IIS:\AppPools\' + $Grupo
$aplicaciones = @((Get-Website | Where-Object ApplicationPool -eq $Grupo) | ForEach-Object { $_.PhysicalPath })
$aplicaciones += @(Get-WebApplication | Where-Object ApplicationPool -eq $Grupo | ForEach-Object { $_.PhysicalPath })
foreach ($rutaAplicacion in $aplicaciones) {
    $resuelta = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($rutaAplicacion)).TrimEnd('\')
    if ($resuelta -ne $DirectorioSitio) { throw 'El grupo ya pertenece a otra aplicacion. Elige un grupo exclusivo.' }
}
if (Test-Path $rutaGrupo) {
    if ((Get-Item $rutaGrupo).processModel.identityType.ToString() -ne 'ApplicationPoolIdentity') { throw 'El grupo tiene otra identidad. No se cambiara automaticamente.' }
}
$marcador = Join-Path $DirectorioPrivado 'preparacion-iis.json'
if (Test-Path -LiteralPath $marcador) {
    $existente = Get-Content -LiteralPath $marcador -Raw | ConvertFrom-Json
    if ($existente.Version -ne 1 -or $existente.Grupo -ne $Grupo -or $existente.Sitio -ne $DirectorioSitio) { throw 'La carpeta privada pertenece a otra preparacion.' }
} elseif ((Test-Path -LiteralPath $DirectorioPrivado) -and @(Get-ChildItem -LiteralPath $DirectorioPrivado -Force).Count -gt 0) {
    throw 'La carpeta privada ya contiene archivos sin identificar. No se modificara; conserva sus llaves y revisa la instalacion existente.'
}
$anterior = $asp.environmentVariables.environmentVariable | Where-Object name -eq 'Instalacion__Directorio' | Select-Object -First 1
if ($anterior -and $anterior.value -ne (Join-Path $DirectorioPrivado 'cnf') -and $aplicaciones.Count -gt 0) {
    foreach ($configGuardada in 'appsettings.Instalacion.json','appsettings.ArranqueRevision.json') {
        if (Test-Path -LiteralPath (Join-Path $anterior.value $configGuardada)) { throw 'La aplicacion ya tiene configuracion privada en otra ruta. No se moveran ni reemplazaran sus llaves.' }
    }
}
if (-not $PSCmdlet.ShouldProcess("$Grupo | $DirectorioSitio | $DirectorioPrivado",'Preparar grupo IIS, permisos y web.config de revision')) { return }
if (-not (Test-Path $rutaGrupo)) { New-WebAppPool -Name $Grupo | Out-Null }
Set-ItemProperty $rutaGrupo -Name managedRuntimeVersion -Value ''
Set-ItemProperty $rutaGrupo -Name managedPipelineMode -Value 'Integrated'
Set-ItemProperty $rutaGrupo -Name enable32BitAppOnWin64 -Value $false
Set-ItemProperty $rutaGrupo -Name processModel.loadUserProfile -Value $true
Set-ItemProperty $rutaGrupo -Name processModel.setProfileEnvironment -Value $true
New-Item -ItemType Directory -Path $DirectorioPrivado -Force | Out-Null
$acl = New-Object Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true,$false)
foreach ($sid in 'S-1-5-18','S-1-5-32-544') {
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule((New-Object Security.Principal.SecurityIdentifier($sid)),'FullControl','ContainerInherit,ObjectInherit','None','Allow')))
}
$acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule("IIS AppPool\$Grupo",'Modify','ContainerInherit,ObjectInherit','None','Allow')))
Set-Acl -LiteralPath $DirectorioPrivado -AclObject $acl
if (-not (Test-Path -LiteralPath $marcador)) {
    [IO.File]::WriteAllText($marcador,(@{Version=1;Grupo=$Grupo;Sitio=$DirectorioSitio} | ConvertTo-Json))
}
New-Item -ItemType Directory -Path (Join-Path $DirectorioPrivado 'logs') -Force | Out-Null
$aclSitio = Get-Acl -LiteralPath $DirectorioSitio
$aclSitio.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule("IIS AppPool\$Grupo",'ReadAndExecute','ContainerInherit,ObjectInherit','None','Allow')))
Set-Acl -LiteralPath $DirectorioSitio -AclObject $aclSitio
$respaldo = Join-Path $DirectorioPrivado ('web.config.' + [Guid]::NewGuid().ToString('N') + '.respaldo')
Copy-Item -LiteralPath $archivoWeb -Destination $respaldo
$asp.SetAttribute('hostingModel','outofprocess')
$asp.SetAttribute('stdoutLogEnabled','false')
$asp.SetAttribute('stdoutLogFile',(Join-Path $DirectorioPrivado 'logs\stdout'))
if (-not $asp.environmentVariables) { [void]$asp.AppendChild($web.CreateElement('environmentVariables')) }
$valores = @{
    ASPNETCORE_ENVIRONMENT='Staging'; Pac__Modo='Deshabilitado';
    Instalacion__ArranqueAutomatico='true'; Instalacion__PermitirSinClaveLocal='true';
    Instalacion__Directorio=(Join-Path $DirectorioPrivado 'cnf');
    AllowedHosts=((@('localhost','127.0.0.1','[::1]',$NombreHost) | Select-Object -Unique) -join ';')
}
foreach ($par in $valores.GetEnumerator()) {
    $nodo = $asp.environmentVariables.environmentVariable | Where-Object name -eq $par.Key | Select-Object -First 1
    if (-not $nodo) { $nodo=$web.CreateElement('environmentVariable'); $nodo.SetAttribute('name',$par.Key); [void]$asp.environmentVariables.AppendChild($nodo) }
    $nodo.SetAttribute('value',$par.Value)
}
$web.Save($archivoWeb)
Write-Output "Preparado. Grupo IIS: $Grupo"
Write-Output "Ruta fisica que debes seleccionar en IIS: $DirectorioSitio"
Write-Output ('Carpeta privada: ' + (Join-Path $DirectorioPrivado 'cnf'))
Write-Output 'Configura el enlace HTTPS y su certificado en IIS. Completa el asistente desde localhost en el servidor.'
Write-Output 'No se ejecutaron scripts SQL, no se generaron llaves y no se cambiaron certificados, enlaces ni firewall.'
