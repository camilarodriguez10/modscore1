<#
.SYNOPSIS
Respalda el proyecto en el repositorio remoto (origin).

.DESCRIPTION
Pasos, en orden:
  1. Comprueba que origin sea el remoto esperado.
  2. Con -Verify: pruebas .NET, pruebas de la SPA, paquetes vulnerables y npm audit. Si algo
     falla, no se respalda.
  3. Agrega los cambios y se niega a versionar lo que no debe salir del equipo: capturas del
     BRD (documentacion interna), imagenes, certificados, llaves y archivos .env.
  4. Si gitleaks esta instalado, escanea lo que se va a confirmar.
  5. Confirma con el mensaje indicado y hace push de la rama actual.

Nunca fuerza el push ni reescribe historia. Ver docs/runbooks/respaldo-en-git.md.

.EXAMPLE
pwsh tools/git/backup.ps1 -Message "Fase 13: revision y remediacion" -Verify
.EXAMPLE
pwsh tools/git/backup.ps1 -Message "WIP documentacion" -NoPush
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateLength(10, 200)]
    [string] $Message,

    [switch] $Verify,

    [switch] $NoPush,

    [string] $ExpectedRemote = 'https://github.com/camilarodriguez10/modscore1.git'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function Invoke-Checked {
    param([string] $File, [string[]] $Arguments, [string] $Failure)

    & $File @Arguments
    if ($LASTEXITCODE -ne 0) { throw $Failure }
}

Push-Location $root
try {
    $remote = (& git remote get-url origin 2>$null)
    if ($LASTEXITCODE -ne 0 -or $remote -ne $ExpectedRemote) {
        throw "El remoto origin es '$remote' y se esperaba '$ExpectedRemote'. Configurelo con: git remote add origin $ExpectedRemote"
    }

    if ($Verify) {
        Write-Host 'Verificando antes de respaldar...'
        Invoke-Checked 'dotnet' @('test', 'ModernizationScore.slnx', '--nologo') 'Pruebas .NET en rojo: no se respalda.'
        Invoke-Checked 'npm' @('--prefix', 'src/web', 'test') 'Pruebas de la SPA en rojo: no se respalda.'

        $vulnerable = & dotnet list ModernizationScore.slnx package --vulnerable --include-transitive
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo revisar la vulnerabilidad de los paquetes.' }
        if ($vulnerable | Where-Object { $_ -match '^\s*>\s' }) {
            $vulnerable | Write-Host
            throw 'Hay paquetes .NET con vulnerabilidades conocidas: no se respalda.'
        }

        Invoke-Checked 'npm' @('--prefix', 'src/web', 'audit', '--audit-level=moderate') 'npm audit encontro vulnerabilidades: no se respalda.'
    }

    Invoke-Checked 'git' @('add', '-A') 'git add fallo.'

    $staged = @(& git diff --cached --name-only)
    if ($staged.Count -eq 0) {
        Write-Host 'No hay cambios que confirmar.'
    }
    else {
        $blocked = @($staged | Where-Object {
            $_ -match '^docs/brd/capturas/' -or
            $_ -match '\.(jpe?g|png|heic|pfx|p12|key|pem)$' -or
            $_ -match '(^|/)\.env(\.|$)'
        })

        if ($blocked.Count -gt 0) {
            & git reset -q
            throw "Estos archivos no se respaldan sin una decision explicita: $($blocked -join ', '). Ver docs/brd/README.md."
        }

        if (Get-Command gitleaks -ErrorAction SilentlyContinue) {
            Invoke-Checked 'gitleaks' @('protect', '--staged', '--config', '.gitleaks.toml', '--redact') 'gitleaks encontro posibles secretos: no se respalda.'
        }
        else {
            Write-Warning 'gitleaks no esta instalado: el escaneo de secretos queda para el pipeline.'
        }

        Invoke-Checked 'git' @('commit', '-m', $Message) 'git commit fallo.'
    }

    if ($NoPush) {
        Write-Host 'Confirmado en local, sin push (-NoPush).'
        return
    }

    $branch = (& git rev-parse --abbrev-ref HEAD).Trim()
    Invoke-Checked 'git' @('push', '--set-upstream', 'origin', $branch) "git push de '$branch' fallo."

    Write-Host "Respaldado: origin/$branch @ $((& git rev-parse --short HEAD).Trim())"
}
finally {
    Pop-Location
}
