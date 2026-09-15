# Runbook — Respaldo en git

**Repositorio:** <https://github.com/camilarodriguez10/modscore1.git> · **Rama principal:** `main`

El proyecto se versiona con git desde el 2026-09-14. Este runbook explica cómo respaldar,
qué queda fuera del repositorio y por qué, y cómo recuperar.

## Decisiones del repositorio (2026-09-15)

1. **Visibilidad: privado.** El código describe el modelo de scoring interno y la
   arquitectura de seguridad.
2. **Qué se versiona: todo lo creado, salvo las capturas del BRD y las imágenes.** Incluye el
   último paquete portable, el estado local de `data/`, los scripts de las sesiones y la
   copia del plan publicado. Detalle en las tablas de abajo.
3. **Autor de los commits: el correo de la cuenta**, no la dirección `…@users.noreply.github.com`.
   Se configura por repositorio, no global:

   ```bash
   git config user.name "camilarodriguez10"
   git config user.email "<correo de la cuenta>"
   ```

4. **Autenticación.** Git Credential Manager (incluido en Git para Windows) abre el navegador
   para iniciar sesión en GitHub. No se guarda ningún token en el repositorio ni en scripts.

El primer push de `main` fue el 2026-09-15.

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
- imágenes (`.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`, `.bmp`, `.heic`, `.tif`, `.ico`);
- certificados y llaves (`.pfx`, `.p12`, `.key`, `.pem`) y archivos `.env`;
- archivos de más de 100 MB, que GitHub rechaza.

Si `gitleaks` está instalado, escanea lo que se va a confirmar con `.gitleaks.toml`.

### Al cerrar una fase

```bash
git tag -a fase-13 -m "Fase 13: revisión y remediación"
git push origin fase-13
```

## Qué no se versiona y dónde vive

| Qué | Por qué | Dónde se respalda |
|---|---|---|
| `docs/brd/capturas/` e imágenes | Documentación interna del negocio | Almacenamiento corporativo |
| `node_modules/`, `bin/`, `obj/`, `dist/`, `*.tsbuildinfo`, resultados de pruebas | Salidas de build y de pruebas | Se regeneran |
| `artifacts/*/` | Carpeta descomprimida que deja el build del paquete | Se regenera; el `.zip` sí se versiona |
| `.claude/settings.local.json`, `.vscode/`, `.idea/` | Configuración personal de herramientas | No aplica |
| `*.pfx`, `*.p12`, `*.key`, `*.pem`, `.env` | Secretos | Gestor de secretos ([rotación](rotacion-de-secretos.md)) |

## Qué se versiona aunque se genere

| Qué | Cuidado |
|---|---|
| `artifacts/*.zip` | Último paquete portable, unos 93 MB. Cada paquete queda **para siempre** en la historia: antes de confirmar uno nuevo, borrar el anterior del árbol. Si los builds se vuelven frecuentes, conviene pasar a GitHub Releases o Git LFS (OP-04) |
| `data/history/`, `data/modernization.db*` | Estado local de prueba, con datos dummy. Cambia al usar la app con los adaptadores de archivos o de base: confirmarlo solo cuando se quiera guardar esa foto. En despliegue el estado vive en su volumen ([recuperación ante desastres](recuperacion-ante-desastres.md)) |
| `data/dummy/` | Datos sintéticos que usan las pruebas |
| `tools/sesiones/` | Scripts históricos de las sesiones de trabajo; no se vuelven a ejecutar |
| `docs/plan/` | Copia del plan publicado como artefacto |
| `.claude/launch.json` | Servidor de desarrollo para el panel de vista previa de Claude Code |

## Recuperar

```bash
git clone https://github.com/camilarodriguez10/modscore1.git
cd modscore1
dotnet test ModernizationScore.slnx
npm --prefix src/web ci --ignore-scripts
npm --prefix src/web run lint
npm --prefix src/web test
```

Las capturas del BRD se copian a `docs/brd/capturas/` desde el almacenamiento corporativo.

## Si se subió algo que no debía

1. No intentar ocultarlo con un commit nuevo: queda en la historia.
2. Si es un secreto, **rotarlo primero** ([rotación de secretos](rotacion-de-secretos.md)).
3. Reescribir la historia (`git filter-repo`) y forzar el push es una decisión del
   responsable del repositorio, coordinada con quienes lo clonaron.
4. Registrar el incidente ([incidente de seguridad](incidente-de-seguridad.md)).
