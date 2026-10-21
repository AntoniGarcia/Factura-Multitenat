param(
    [Parameter(Mandatory=$true)][string]$Publicado,
    [Parameter(Mandatory=$true)][string]$Privado,
    [Parameter(Mandatory=$true)][string]$Salida
)
$ErrorActionPreference = 'Stop'
$resultado = [ordered]@{ Sitio='FacturacionRevisionLocal20261020'; Url='https://localhost:7443/operador'; Privado=$Privado; Error=$null }
try {
    Import-Module WebAdministration
    if (-not (Get-WebGlobalModule | Where-Object Name -eq 'AspNetCoreModuleV2')) { throw 'Falta AspNetCoreModuleV2.' }
    $Publicado = [IO.Path]::GetFullPath($Publicado).TrimEnd('\')
    $Privado = [IO.Path]::GetFullPath($Privado).TrimEnd('\')
    if ($Privado.StartsWith($Publicado + '\', [StringComparison]::OrdinalIgnoreCase) -or $Privado -eq $Publicado) { throw 'La carpeta privada debe quedar fuera del sitio.' }
    if (-not (Test-Path -LiteralPath (Join-Path $Publicado 'Facturacion.Server.dll'))) { throw 'Falta la publicacion.' }
    $nombre = $resultado.Sitio
    if (Test-Path "IIS:\Sites\$nombre") { throw 'Ya existe el sitio de prueba; no se reemplaza.' }
    if (Test-Path "IIS:\AppPools\$nombre") { throw 'Ya existe el grupo de prueba; no se reemplaza.' }
    if (Get-WebBinding | Where-Object { $_.bindingInformation -match ':7443:' }) { throw 'El puerto IIS 7443 ya esta en uso.' }
    New-Item -ItemType Directory -Path $Privado -Force | Out-Null
    $acl = New-Object Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($sid in 'S-1-5-18','S-1-5-32-544') {
        $regla = New-Object Security.AccessControl.FileSystemAccessRule((New-Object Security.Principal.SecurityIdentifier($sid)), 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.AddAccessRule($regla)
    }
    New-WebAppPool -Name $nombre | Out-Null
    Set-ItemProperty "IIS:\AppPools\$nombre" -Name managedRuntimeVersion -Value ''
    Set-ItemProperty "IIS:\AppPools\$nombre" -Name enable32BitAppOnWin64 -Value $false
    Set-ItemProperty "IIS:\AppPools\$nombre" -Name processModel.loadUserProfile -Value $true
    Set-ItemProperty "IIS:\AppPools\$nombre" -Name processModel.setProfileEnvironment -Value $true
    $reglaPool = New-Object Security.AccessControl.FileSystemAccessRule("IIS AppPool\$nombre", 'Modify', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
    $acl.AddAccessRule($reglaPool)
    Set-Acl -LiteralPath $Privado -AclObject $acl
    $aclSitio = Get-Acl -LiteralPath $Publicado
    $aclSitio.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule("IIS AppPool\$nombre", 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
    Set-Acl -LiteralPath $Publicado -AclObject $aclSitio
    [xml]$web = Get-Content -LiteralPath (Join-Path $Publicado 'web.config')
    $asp = $web.configuration.location.'system.webServer'.aspNetCore
    $asp.SetAttribute('hostingModel', 'outofprocess')
    $asp.SetAttribute('stdoutLogEnabled', 'true')
    $asp.SetAttribute('stdoutLogFile', (Join-Path $Privado 'logs\stdout'))
    New-Item -ItemType Directory -Path (Join-Path $Privado 'logs') -Force | Out-Null
    $variables = $web.CreateElement('environmentVariables')
    foreach ($par in @{
        ASPNETCORE_ENVIRONMENT='Staging'; Instalacion__ArranqueAutomatico='true';
        Instalacion__Directorio=(Join-Path $Privado 'cnf'); Pac__Modo='Deshabilitado'; AllowedHosts='localhost;127.0.0.1'
    }.GetEnumerator()) {
        $nodo = $web.CreateElement('environmentVariable')
        $nodo.SetAttribute('name', $par.Key)
        $nodo.SetAttribute('value', $par.Value)
        [void]$variables.AppendChild($nodo)
    }
    [void]$asp.AppendChild($variables)
    $web.Save((Join-Path $Publicado 'web.config'))
    $certificado = New-SelfSignedCertificate -DnsName localhost -CertStoreLocation Cert:\LocalMachine\My -FriendlyName 'Facturacion IIS revision local 20261020' -NotAfter (Get-Date).AddMonths(3)
    $raiz = New-Object Security.Cryptography.X509Certificates.X509Store('Root','LocalMachine')
    $raiz.Open('ReadWrite')
    try { $raiz.Add((New-Object Security.Cryptography.X509Certificates.X509Certificate2 -ArgumentList @(,$certificado.RawData))) } finally { $raiz.Close() }
    New-Website -Name $nombre -PhysicalPath $Publicado -ApplicationPool $nombre -IPAddress '127.0.0.1' -Port 7443 -Ssl | Out-Null
    (Get-WebBinding -Name $nombre -Protocol https).AddSslCertificate($certificado.Thumbprint, 'My')
    New-WebBinding -Name $nombre -Protocol https -IPAddress '[::1]' -Port 7443
    (Get-WebBinding -Name $nombre -Protocol https | Where-Object bindingInformation -eq '[::1]:7443:').AddSslCertificate($certificado.Thumbprint, 'My')
    $resultado.Certificado = $certificado.Thumbprint
} catch { $resultado.Error = $_.Exception.Message }
[IO.File]::WriteAllText($Salida, ($resultado | ConvertTo-Json -Depth 4))
if ($resultado.Error) { exit 1 }
