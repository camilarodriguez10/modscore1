@echo off
rem Modernization Score - paquete de PRUEBA. Doble clic para arrancar con datos generados.
rem Opciones: start.cmd -Data File    (carga de archivos)
rem           start.cmd -Port 5200     (otro puerto)
rem La politica de ejecucion solo se omite para este proceso de PowerShell.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start.ps1" %*
