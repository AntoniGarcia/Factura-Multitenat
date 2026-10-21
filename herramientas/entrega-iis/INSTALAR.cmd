@echo off
setlocal DisableDelayedExpansion
title Instalar Facturacion para revision
powershell.exe -NoProfile -File "%~dp0INSTALAR.ps1" -Pausar
exit /b %errorlevel%
