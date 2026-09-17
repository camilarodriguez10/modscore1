<#
.SYNOPSIS
Mide la simulacion de reglas con la capa System fijada a pocos nucleos, como en un pod.

.DESCRIPTION
En una maquina de desarrollo con muchos nucleos, ocho simulaciones a la vez no llegan a
competir con nada. En un pod con uno o dos CPU si. Este script arranca la capa System ya
compilada en Release, la fija a los nucleos indicados y corre dos mediciones:

  1. solo simulaciones, con la concurrencia pedida;
  2. lecturas de score mientras las simulaciones corren, que es lo que pagan los demas
     usuarios (TD-06).

La herramienta de carga corre sin fijar, en los nucleos restantes, para no medirse a si
misma. Al terminar detiene la capa System.

.PARAMETER Cores
Cuantos nucleos ve la capa System. Por defecto 2.

.PARAMETER Concurrency
Simulaciones y lecturas concurrentes. Por defecto 8.

.EXAMPLE
dotnet build -c Release src/system/Modernization.SystemApi
dotnet build -c Release tools/Modernization.LoadTest
powershell -ExecutionPolicy Bypass -File tools/Modernization.LoadTest/medir-en-dos-nucleos.ps1
#>
[CmdletBinding()]
param(
    [ValidateRange(1, 64)]
    [int] $Cores = 2,

    [ValidateRange(1, 64)]
    [int] $Concurrency = 8
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$systemDir = Join-Path $root 'src\system\Modernization.SystemApi\bin\Release\net10.0'
$systemExe = Join-Path $systemDir 'Modernization.SystemApi.exe'

if (-not (Test-Path -LiteralPath $systemExe)) {
    throw "No existe $systemExe. Compilar antes: dotnet build -c Release src/system/Modernization.SystemApi"
}

if ($Cores -ge [Environment]::ProcessorCount) {
    throw "El equipo tiene $([Environment]::ProcessorCount) nucleos: pedir menos para que la herramienta de carga no compita."
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5101'
$env:DataSource__Provider = 'Dummy'
$env:Logging__LogLevel__Default = 'Warning'
# El runtime dimensiona el pool de hilos y Environment.ProcessorCount con esto: sin el,
# el proceso fijado a dos nucleos se cree con todos los del equipo.
$env:DOTNET_PROCESSOR_COUNT = "$Cores"

$system = Start-Process -FilePath $systemExe -WorkingDirectory $systemDir -PassThru -WindowStyle Hidden

try {
    $system.ProcessorAffinity = [IntPtr]((1 -shl $Cores) - 1)

    $deadline = (Get-Date).AddSeconds(60)
    do {
        Start-Sleep -Milliseconds 500
        try { $ready = (Invoke-WebRequest -UseBasicParsing -TimeoutSec 2 -Uri 'http://127.0.0.1:5101/health').StatusCode -eq 200 } catch { $ready = $false }
    } until ($ready -or (Get-Date) -gt $deadline)

    if (-not $ready) { throw 'La capa System no respondio en 60 segundos.' }

    Remove-Item Env:DOTNET_PROCESSOR_COUNT
    $load = @('run', '-c', 'Release', '--no-build', '--project', (Join-Path $root 'tools\Modernization.LoadTest'), '--')

    Write-Host "== Capa System en $Cores nucleos, $Concurrency concurrentes =="
    Write-Host ''
    Write-Host '-- 1. Solo simulaciones'
    & dotnet @load --scenario 'rules/simulate' --concurrency $Concurrency --duration 20 | Select-Object -Last 1

    Write-Host ''
    Write-Host '-- 2. Lecturas mientras corren las simulaciones'
    $simulations = Start-Job -ScriptBlock {
        param($arguments, $concurrency)
        [Console]::OutputEncoding = [Text.Encoding]::UTF8
        & dotnet @arguments --scenario 'rules/simulate' --concurrency $concurrency --duration 35 | Select-Object -Last 1
    } -ArgumentList (, $load), $Concurrency

    Start-Sleep -Seconds 8
    & dotnet @load --scenario '{eai}/score' --concurrency $Concurrency --duration 20 | Select-Object -Last 1
    Receive-Job -Job $simulations -Wait -AutoRemoveJob
}
finally {
    if (-not $system.HasExited) { Stop-Process -Id $system.Id -Force }
}
