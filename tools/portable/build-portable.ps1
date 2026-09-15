<#
.SYNOPSIS
Genera el paquete portable de Modernization Score para probarlo en otro equipo Windows.

.DESCRIPTION
Paso intermedio antes de publicar: un .zip que se descomprime y se arranca con doble clic,
sin Visual Studio, sin Node y —en la variante autocontenida— sin .NET instalado.

NO es un despliegue. Corre en el entorno Development con el esquema de autenticacion de
desarrollo, que no autentica a nadie. Sirve para probar la aplicacion en un equipo, no para
exponerla en una red.

Contenido:
  system\   capa System (Modernization.SystemApi.exe), un solo archivo
  channel\  capa Channel (Modernization.ChannelApi.exe) con la SPA en wwwroot
  samples\  los CSV dummy, para probar la carga con el proveedor de archivos
  start.cmd / start.ps1 / README.txt

.PARAMETER Variant
SelfContained (por defecto): no requiere .NET instalado; unos 45 MB por capa comprimidos.
FrameworkDependent: requiere el runtime de ASP.NET Core 10 en el equipo; mucho mas liviano.

.PARAMETER Runtime
Identificador de runtime de destino. Por defecto win-x64.

.EXAMPLE
pwsh tools/portable/build-portable.ps1
.EXAMPLE
pwsh tools/portable/build-portable.ps1 -Variant FrameworkDependent
#>
[CmdletBinding()]
param(
    [ValidateSet('SelfContained', 'FrameworkDependent')]
    [string] $Variant = 'SelfContained',

    [ValidateSet('win-x64', 'win-arm64')]
    [string] $Runtime = 'win-x64',

    [string] $Output
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $Output) { $Output = Join-Path $root 'artifacts' }

$stamp = Get-Date -Format 'yyyyMMdd-HHmm'
$name = "modscore-portable-$Runtime-$($Variant.ToLowerInvariant())-$stamp"
$staging = Join-Path $Output $name
$zip = "$staging.zip"

function Invoke-Checked {
    param([string] $File, [string[]] $Arguments)

    & $File @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "'$File $($Arguments -join ' ')' termino con codigo $LASTEXITCODE."
    }
}

if (Test-Path $staging) { Remove-Item -Recurse -Force -LiteralPath $staging }
New-Item -ItemType Directory -Force -Path $staging | Out-Null

# ------------------------------------------------------------------- SPA
Write-Host '1/4  SPA: build de produccion (sin source maps)'
Push-Location (Join-Path $root 'src\web')
try {
    # npm ci instala exactamente el lock y sin scripts de instalacion; solo si falta.
    if (-not (Test-Path 'node_modules')) { Invoke-Checked 'npm' @('ci', '--ignore-scripts') }
    Invoke-Checked 'npm' @('audit', '--audit-level=moderate', '--omit=dev')
    Invoke-Checked 'npm' @('run', 'build')
}
finally {
    Pop-Location
}

# ------------------------------------------------------------ capas .NET
$selfContained = $Variant -eq 'SelfContained'
$publish = @(
    '-c', 'Release',
    '-r', $Runtime,
    "--self-contained=$($selfContained.ToString().ToLowerInvariant())",
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:DebugType=none',
    '-p:DebugSymbols=false',
    '-p:GenerateDocumentationFile=false',
    # Solo los recursos en ingles: el sitio esta en ingles y cada idioma pesa.
    '-p:SatelliteResourceLanguages=en',
    '--nologo'
)
if ($selfContained) { $publish += '-p:EnableCompressionInSingleFile=true' }

# Publicar con -r agrega a los lock files una seccion por runtime y Microsoft.NET.ILLink.Tasks.
# La restauracion del CI (--locked-mode) fallaria con ellos: se guardan y se devuelven al final.
$lockFiles = @(Get-ChildItem -Path (Join-Path $root 'src/*/*/packages.lock.json') -File)
$savedLocks = @{}
foreach ($lock in $lockFiles) { $savedLocks[$lock.FullName] = [IO.File]::ReadAllBytes($lock.FullName) }

try {
    Write-Host "2/4  Capa System ($Variant, $Runtime)"
    Invoke-Checked 'dotnet' (@('publish', (Join-Path $root 'src\system\Modernization.SystemApi\Modernization.SystemApi.csproj'), '-o', (Join-Path $staging 'system')) + $publish)

    Write-Host "3/4  Capa Channel ($Variant, $Runtime) con la SPA"
    Invoke-Checked 'dotnet' (@('publish', (Join-Path $root 'src\channel\Modernization.ChannelApi\Modernization.ChannelApi.csproj'), '-o', (Join-Path $staging 'channel')) + $publish)
}
finally {
    foreach ($entry in $savedLocks.GetEnumerator()) { [IO.File]::WriteAllBytes($entry.Key, $entry.Value) }
}

$wwwroot = Join-Path $staging 'channel\wwwroot'
New-Item -ItemType Directory -Force -Path $wwwroot | Out-Null
Copy-Item -Recurse -Force -Path (Join-Path $root 'src\web\dist\*') -Destination $wwwroot

# Ni simbolos ni mapas en el paquete: entregan el codigo fuente.
Get-ChildItem -Recurse -Path $staging -Include '*.pdb', '*.map', '*.xml' -File | Where-Object { $_.Name -notlike '*.deps.xml' } | Remove-Item -Force

# ------------------------------------------------------ lanzador y datos
Copy-Item -Force -Path (Join-Path $PSScriptRoot 'start.ps1'), (Join-Path $PSScriptRoot 'start.cmd') -Destination $staging
Copy-Item -Force -Path (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $staging 'README.txt')

$samples = New-Item -ItemType Directory -Force -Path (Join-Path $staging 'samples')
Copy-Item -Force -Path (Join-Path $root 'data\dummy\*.csv') -Destination $samples

Write-Host '4/4  Comprimiendo'
if (Test-Path $zip) { Remove-Item -Force -LiteralPath $zip }
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip -CompressionLevel Optimal

$size = (Get-Item $zip).Length / 1MB
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zip).Hash.ToLowerInvariant()

Write-Host ''
Write-Host ("Paquete: {0} ({1:N1} MB)" -f $zip, $size)
Write-Host "SHA-256: $hash"
Write-Host 'En el otro equipo: descomprimir y ejecutar start.cmd. Ver README.txt.'
