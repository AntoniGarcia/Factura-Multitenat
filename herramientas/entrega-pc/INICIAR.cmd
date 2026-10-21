@echo off
setlocal
where dotnet >nul 2>&1
if errorlevel 1 goto falta_dotnet
dotnet --list-sdks | findstr /B "10\." >nul
if errorlevel 1 goto falta_dotnet
dotnet --list-runtimes | findstr /B "Microsoft.AspNetCore.App 10\." >nul
if errorlevel 1 goto falta_dotnet
if not exist "%~dp0sitio\Facturacion.Server.dll" goto falta_sitio
if not exist "%~dp0sitio\wwwroot\index.html" goto falta_sitio
if not exist "%~dp0sitio\CatalogosSAT\cfdv40.xsd" goto falta_sitio
if /I "%~1"=="/comprobar" (
  echo Requisitos y archivos principales disponibles.
  exit /b 0
)
dotnet dev-certs https --check --trust >nul 2>&1
if not errorlevel 1 goto certificado_listo
echo Se necesita un certificado HTTPS local confiable para localhost.
echo Windows puede solicitar confirmacion para confiar en este certificado.
choice /C SN /N /M "Deseas prepararlo ahora? [S/N]: "
if errorlevel 2 exit /b 1
dotnet dev-certs https --trust
if errorlevel 1 goto error
:certificado_listo
if not defined FACTURACION_REVISION_DATOS set "FACTURACION_REVISION_DATOS=%LOCALAPPDATA%\FacturacionRevisionPC"
if not defined FACTURACION_REVISION_PUERTO set "FACTURACION_REVISION_PUERTO=7245"
set "ConnectionStrings__BaseDeDatos="
pushd "%~dp0sitio"
if errorlevel 1 goto error
dotnet Facturacion.Server.dll --environment Staging --Instalacion:PrepararRevision=true "--Instalacion:Directorio=%FACTURACION_REVISION_DATOS%\cnf" --Pac:Modo=Deshabilitado
if errorlevel 1 goto error_con_carpeta
echo.
echo Abre https://localhost:%FACTURACION_REVISION_PUERTO%/operador cuando aparezca Now listening.
echo La clave del asistente esta en:
echo %FACTURACION_REVISION_DATOS%\cnf\clave-instalacion.txt
echo Conserva esta ventana abierta. Para detener el sistema presiona Ctrl+C.
echo.
dotnet Facturacion.Server.dll --environment Staging --Instalacion:PrepararRevision=false --Instalacion:SoloConexion=false "--Instalacion:Directorio=%FACTURACION_REVISION_DATOS%\cnf" --Pac:Modo=Deshabilitado "--urls=https://localhost:%FACTURACION_REVISION_PUERTO%"
if errorlevel 1 goto error_con_carpeta
popd
exit /b 0
:falta_dotnet
echo Instala .NET 10 SDK para Windows desde:
echo https://dotnet.microsoft.com/download/dotnet/10.0
goto error
:falta_sitio
echo Faltan archivos. Extrae todo el ZIP antes de ejecutar INICIAR.cmd.
goto error
:error_con_carpeta
popd
:error
echo No se pudo iniciar. Revisa el mensaje anterior y el archivo LEEME.md.
pause
exit /b 1
