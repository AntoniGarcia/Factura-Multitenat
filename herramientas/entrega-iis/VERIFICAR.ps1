param([string]$DirectorioSitio = (Join-Path $PSScriptRoot 'sitio'))
$ErrorActionPreference = 'Stop'
$requeridos = @('Facturacion.Server.dll','Facturacion.Server.runtimeconfig.json','web.config','wwwroot\index.html',
    'CatalogosSAT\catCFDI_V_4_20260806.xls','CatalogosSAT\CatalogosCartaPorte31.xls',
    'CatalogosSAT\c_FraccionArancelaria_20240513.xls','CatalogosSAT\c_INCOTERM20.xls','CatalogosSAT\c_UnidadAduana20.xls',
    'CatalogosSAT\cfdv40.xsd','CatalogosSAT\catCFDI.xsd','CatalogosSAT\tdCFDI.xsd',
    'CatalogosSAT\CartaPorte31.xsd','CatalogosSAT\catCartaPorte.xsd',
    'CatalogosSAT\ComercioExterior20.xsd','CatalogosSAT\catComExt.xsd',
    'CatalogosSAT\Pagos20.xsd','CatalogosSAT\notariospublicos.xsd','CatalogosSAT\implocal.xsd',
    'CatalogosSAT\cadenaoriginal_4_0.xslt','CatalogosSAT\nomina12.xsd','CatalogosSAT\nomina12.xslt','CatalogosSAT\catNomina.xsd','CatalogosSAT\catPagos.xsd')
foreach ($archivo in $requeridos) {
    if (-not (Test-Path -LiteralPath (Join-Path $DirectorioSitio $archivo) -PathType Leaf)) { throw "Falta $archivo" }
}
if (([IO.File]::ReadAllText((Join-Path $DirectorioSitio 'wwwroot\index.html'))).Contains('#[')) { throw 'Hay marcadores HTML sin resolver.' }
if (Test-Path -LiteralPath (Join-Path $DirectorioSitio 'appsettings.Development.json')) { throw 'No se debe entregar configuracion de desarrollo.' }
[xml]$configuracion = Get-Content -LiteralPath (Join-Path $DirectorioSitio 'web.config')
$asp = $configuracion.configuration.location.'system.webServer'.aspNetCore
if ($asp.hostingModel -ne 'outofprocess') { throw 'El paquete de revision requiere outofprocess.' }
$variables = @{}
foreach ($elemento in $asp.environmentVariables.environmentVariable) { $variables[$elemento.name] = $elemento.value }
if ($variables.ASPNETCORE_ENVIRONMENT -ne 'Staging' -or $variables.Pac__Modo -ne 'Deshabilitado' -or $variables.Instalacion__ArranqueAutomatico -ne 'true') { throw 'La configuracion no coincide con revision sin PAC.' }
$privado = $variables.Instalacion__Directorio
if (-not [IO.Path]::IsPathRooted($privado)) { throw 'La carpeta privada debe ser absoluta.' }
$raiz = [IO.Path]::GetFullPath($DirectorioSitio).TrimEnd('\')
if ($privado -eq $raiz -or ([IO.Path]::GetFullPath($privado)).StartsWith($raiz + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'La carpeta privada no puede quedar dentro del sitio.' }
$brotli = @(Get-ChildItem -LiteralPath (Join-Path $DirectorioSitio 'wwwroot') -Recurse -File -Filter '*.br').Count
if ($brotli -eq 0) { throw 'Faltan los archivos comprimidos Brotli.' }
Write-Output "Archivos y configuracion de revision correctos. Recursos Brotli: $brotli."
Write-Output 'Esta comprobacion no instala IIS, no prueba SQL y no valida DNS ni HTTPS del servidor.'
