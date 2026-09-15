#!/usr/bin/env bash
# Respalda el proyecto en el repositorio remoto (origin). Equivalente a backup.ps1.
#
#   tools/git/backup.sh "Fase 13: revision y remediacion" --verify
#   tools/git/backup.sh "WIP documentacion" --no-push
#
# Nunca fuerza el push ni reescribe historia. Ver docs/runbooks/respaldo-en-git.md.
set -euo pipefail

EXPECTED_REMOTE="https://github.com/camilarodriguez10/modscore1.git"

message="${1:-}"
shift || true

verify=false
push=true
for arg in "$@"; do
  case "$arg" in
    --verify) verify=true ;;
    --no-push) push=false ;;
    *) echo "Opcion desconocida: $arg" >&2; exit 2 ;;
  esac
done

if [ "${#message}" -lt 10 ]; then
  echo "Indique un mensaje de al menos 10 caracteres." >&2
  exit 2
fi

cd "$(dirname "$0")/../.."

remote="$(git remote get-url origin 2>/dev/null || true)"
if [ "$remote" != "$EXPECTED_REMOTE" ]; then
  echo "El remoto origin es '$remote' y se esperaba '$EXPECTED_REMOTE'." >&2
  exit 1
fi

if [ "$verify" = true ]; then
  echo "Verificando antes de respaldar..."
  dotnet test ModernizationScore.slnx --nologo
  npm --prefix src/web test
  if dotnet list ModernizationScore.slnx package --vulnerable --include-transitive | grep -Eq '^\s*>\s'; then
    echo "Hay paquetes .NET con vulnerabilidades conocidas: no se respalda." >&2
    exit 1
  fi
  npm --prefix src/web audit --audit-level=moderate
fi

git add -A

if git diff --cached --quiet; then
  echo "No hay cambios que confirmar."
else
  blocked="$(git diff --cached --name-only | grep -E '^docs/brd/capturas/|\.(jpe?g|png|heic|pfx|p12|key|pem)$|(^|/)\.env(\.|$)' || true)"
  if [ -n "$blocked" ]; then
    git reset -q
    echo "Estos archivos no se respaldan sin una decision explicita:" >&2
    echo "$blocked" >&2
    exit 1
  fi

  if command -v gitleaks >/dev/null 2>&1; then
    gitleaks protect --staged --config .gitleaks.toml --redact
  else
    echo "Aviso: gitleaks no esta instalado; el escaneo de secretos queda para el pipeline." >&2
  fi

  git commit -m "$message"
fi

if [ "$push" = false ]; then
  echo "Confirmado en local, sin push (--no-push)."
  exit 0
fi

branch="$(git rev-parse --abbrev-ref HEAD)"
git push --set-upstream origin "$branch"
echo "Respaldado: origin/$branch @ $(git rev-parse --short HEAD)"
