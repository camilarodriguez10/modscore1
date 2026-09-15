# Runbook — Respaldo en git

**Repositorio:** <https://github.com/camilarodriguez10/modscore1.git> · **Rama principal:** `main`

El proyecto se versiona con git desde el 2026-09-14. Este runbook explica cómo respaldar,
qué queda fuera del repositorio y por qué, y cómo recuperar.

## Antes del primer push

1. **Confirmar la visibilidad del repositorio.** Debe ser **privado**: el código describe el
   modelo de scoring interno y la arquitectura de seguridad.
2. **Autenticación.** Git Credential Manager (incluido en Git para Windows) abre el navegador
   para iniciar sesión en GitHub. No se guarda ningún token en el repositorio ni en scripts.
3. **Autor de los commits.** Se configura por repositorio, no global:

   ```bash
   git config user.name "camilarodriguez10"
   git config user.email "<correo de la cuenta>"
   ```

   El correo queda en los metadatos de cada commit publicado. GitHub permite usar la
   dirección `…@users.noreply.github.com` si no se quiere exponer el correo real.

## Respaldar

```powershell
pwsh tools/git/backup.ps1 -Message "Fase 13: revisión y remediación" -Verify
```

```bash
tools/git/backup.sh "Fase 13: revisión y remediación" --verify
```

| Opción | Efecto |
|---|---|
| `-Verify` / `--verify` | Corre pruebas .NET y de la SPA, revisa paquetes vulnerables y `npm audit`. Si algo falla no respalda |
| `-NoPush` / `--no-push` | Confirma en local sin subir |

El script **nunca** fuerza el push ni reescribe historia, y se niega a confirmar:

- `docs/brd/capturas/` (capturas del BRD y del dashboard: documentación interna);
- imágenes, certificados y llaves (`.jpg`, `.png`, `.pfx`, `.p12`, `.key`, `.pem`);
- archivos `.env`.

Si `gitleaks` está instalado, escanea lo que se va a confirmar con `.gitleaks.toml`.

### Al cerrar una fase

```bash
git tag -a fase-13 -m "Fase 13: revisión y remediación"
git push origin fase-13
```

## Qué no se versiona y dónde vive

| Qué | Por qué | Dónde se respalda |
|---|---|---|
| `docs/brd/capturas/` | Documentación interna del negocio | Almacenamiento corporativo. Solo se sube con `git add -f` si el repositorio es privado y el negocio lo autoriza |
| `data/history/`, `data/.rules/`, `*.db` | Estado de ejecución: cargas y versiones de reglas publicadas | Volumen persistente del despliegue y su propio respaldo ([recuperación ante desastres](recuperacion-ante-desastres.md)) |
| `artifacts/` | Paquetes portables generados | Se regeneran con `tools/portable/build-portable.ps1` |
| `node_modules/`, `bin/`, `obj/`, `dist/` | Salidas de build | Se regeneran |
| `.claude/`, `.vscode/`, `.idea/` | Configuración local de herramientas | No aplica |

Los datos dummy (`data/dummy/`) **sí** se versionan: son sintéticos y las pruebas los usan.

## Recuperar

```bash
git clone https://github.com/camilarodriguez10/modscore1.git
cd modscore1
dotnet test ModernizationScore.slnx
npm --prefix src/web ci --ignore-scripts
npm --prefix src/web test
```

Las capturas del BRD se copian a `docs/brd/capturas/` desde el almacenamiento corporativo.

## Si se subió algo que no debía

1. No intentar ocultarlo con un commit nuevo: queda en la historia.
2. Si es un secreto, **rotarlo primero** ([rotación de secretos](rotacion-de-secretos.md)).
3. Reescribir la historia (`git filter-repo`) y forzar el push es una decisión del
   responsable del repositorio, coordinada con quienes lo clonaron.
4. Registrar el incidente ([incidente de seguridad](incidente-de-seguridad.md)).
