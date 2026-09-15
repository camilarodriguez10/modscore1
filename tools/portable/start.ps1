<#
.SYNOPSIS
Arranca Modernization Score en este equipo para probarlo. NO es un despliegue.

.DESCRIPTION
Levanta la capa System y la capa Channel solo en la interfaz local (127.0.0.1) y abre el
navegador. Usa el entorno Development y el esquema de autenticacion de desarrollo: cualquiera
que llegue al puerto entra como administrador. Por eso nada escucha fuera de este equipo.

El token de servicio entre las dos capas se genera en cada arranque y se entrega como archivo,
igual que en despliegue lo hace el gestor de secretos; se borra al detener.

.PARAMETER Data
Dummy (por defecto): 250 aplicaciones generadas en memoria.
File: carga de archivos; la carpeta data\ guarda las cargas. Los CSV de samples\ sirven para
la primera carga desde la pantalla Data.

.PARAMETER Port
Puerto de la aplicacion. La capa System usa el siguiente.
#>
[CmdletBinding()]
param(
    [ValidateSet('Dummy', 'File')]
    [string] $Data = 'Dummy',

    [ValidateRange(1024, 65000)]
    [int] $Port = 5100,

    [switch] $NoBrowser
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$here = $PSScriptRoot
$systemPort = $Port + 1
$systemExe = Join-Path $here 'system\Modernization.SystemApi.exe'
$channelExe = Join-Path $here 'channel\Modernization.ChannelApi.exe'

foreach ($exe in $systemExe, $channelExe) {
    if (-not (Test-Path -LiteralPath $exe)) { throw "No se encontro $exe. Descomprima el paquete completo." }
}

Write-Host ''
Write-Host '  Modernization Score - paquete de PRUEBA' -ForegroundColor Cyan
Write-Host '  Autenticacion de desarrollo: no usar con datos reales ni exponer en una red.' -ForegroundColor Yellow
Write-Host ''

# Token de servicio aleatorio por ejecucion, como archivo (Secrets:Directory).
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
$token = [Convert]::ToBase64String($bytes)

$secrets = Join-Path ([IO.Path]::GetTempPath()) ('modscore-secrets-' + [guid]::NewGuid().ToString('N'))
$systemSecrets = New-Item -ItemType Directory -Path (Join-Path $secrets 'system')
$channelSecrets = New-Item -ItemType Directory -Path (Join-Path $secrets 'channel')
[IO.File]::WriteAllText((Join-Path $systemSecrets.FullName 'ServiceToken__Token'), $token)
[IO.File]::WriteAllText((Join-Path $channelSecrets.FullName 'SystemApi__ServiceToken'), $token)

$processes = @()

try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:Logging__LogLevel__Default = 'Warning'

    # ------------------------------------------------------------ System
    $env:Secrets__Directory = $systemSecrets.FullName
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$systemPort"
    $env:DataSource__Provider = $Data

    if ($Data -eq 'File') {
        $dataDirectory = Join-Path $here 'data'
        New-Item -ItemType Directory -Force -Path $dataDirectory | Out-Null
        $env:DataSource__Directory = $dataDirectory
    }

    $processes += Start-Process -FilePath $systemExe -WorkingDirectory (Split-Path $systemExe) -PassThru -NoNewWindow

    # ----------------------------------------------------------- Channel
    $env:Secrets__Directory = $channelSecrets.FullName
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$Port"
    $env:SystemApi__BaseUrl = "http://127.0.0.1:$systemPort"
    Remove-Item Env:DataSource__Provider -ErrorAction SilentlyContinue
    Remove-Item Env:DataSource__Directory -ErrorAction SilentlyContinue

    $processes += Start-Process -FilePath $channelExe -WorkingDirectory (Split-Path $channelExe) -PassThru -NoNewWindow

    Write-Host 'Esperando a que las dos capas respondan...'
    $deadline = (Get-Date).AddSeconds(90)
    $ready = $false

    while (-not $ready -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        try {
            $system = Invoke-WebRequest -UseBasicParsing -TimeoutSec 2 -Uri "http://127.0.0.1:$systemPort/health"
            $channel = Invoke-WebRequest -UseBasicParsing -TimeoutSec 2 -Uri "http://127.0.0.1:$Port/health"
            $ready = $system.StatusCode -eq 200 -and $channel.StatusCode -eq 200
        }
        catch {
            if ($processes | Where-Object { $_.HasExited }) { throw 'Una de las capas se detuvo al arrancar. Revise los mensajes de arriba.' }
        }
    }

    if (-not $ready) { throw 'Las capas no respondieron en 90 segundos.' }

    $url = "http://localhost:$Port/"
    Write-Host ''
    Write-Host "Listo: $url  (datos: $Data)" -ForegroundColor Green
    if (-not $NoBrowser) { Start-Process $url }

    Write-Host 'Presione Enter para detener.'
    [void][Console]::ReadLine()
}
finally {
    foreach ($process in $processes) {
        if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    }

    Remove-Item -Recurse -Force -LiteralPath $secrets -ErrorAction SilentlyContinue
    Write-Host 'Detenido.'
}
