from patchlib import patch

patch(".gitignore", [
("""# Paquete portable: el .zip se versiona; la carpeta descomprimida que deja el build, no.
# GitHub rechaza archivos de mas de 100 MB (los scripts de tools/git lo revisan).
artifacts/*/""",
"""# Salidas de build locales. El paquete portable que vivia aqui se retiro el 2026-09-27.
artifacts/"""),
])

patch(r".github\workflows\ci.yml", [
("""# Un push o PR que solo cambia documentacion, los scripts historicos, el paquete portable
# o el estado local de data/ no dispara este workflow""",
"""# Un push o PR que solo cambia documentacion, los scripts historicos o el estado local de
# data/ no dispara este workflow"""),
("""      - "tools/sesiones/**"
      - "artifacts/**"
      - "data/history/**"
      - "data/*.db*"
      - ".claude/**"
  pull_request:""",
"""      - "tools/sesiones/**"
      - "data/history/**"
      - "data/*.db*"
      - ".claude/**"
  pull_request:"""),
("""      - "tools/sesiones/**"
      - "artifacts/**"
      - "data/history/**"
      - "data/*.db*"
      - ".claude/**"
""",
"""      - "tools/sesiones/**"
      - "data/history/**"
      - "data/*.db*"
      - ".claude/**"
"""),
])

patch(r"src\web\playwright.config.ts", [
(""" * que ya esten corriendo. Para probar un paquete portable ya arrancado:""",
 """ * que ya esten corriendo. Para probar un sitio ya arrancado —por ejemplo, la capa Channel
 * sirviendo el sitio compilado con --webroot—:"""),
])

patch(r"docs\runbooks\respaldo-en-git.md", [
("""2. **Qué se versiona: todo lo creado, salvo las capturas del BRD y las imágenes.** Incluye el
   último paquete portable, el estado local de `data/`, los scripts de las sesiones y la
   copia del plan publicado. Detalle en las tablas de abajo.""",
"""2. **Qué se versiona: todo lo creado, salvo las capturas del BRD y las imágenes.** Incluye el
   estado local de `data/`, los scripts de las sesiones y la copia del plan publicado.
   Detalle en las tablas de abajo. El paquete portable se versionó hasta el 2026-09-27, cuando
   se retiró: la historia conserva sus `.zip` anteriores."""),
("""| `artifacts/*.zip` | Último paquete portable, unos 93 MB. Cada paquete queda **para siempre** en la historia: antes de confirmar uno nuevo, borrar el anterior del árbol. Si los builds se vuelven frecuentes, conviene pasar a GitHub Releases o Git LFS (OP-04) |
""", ""),
])

patch(r"docs\security\modelo-de-amenazas.md", [
("""| **E** | El paquete portable, con autenticación de desarrollo, queda expuesto en una red |""",
 """| **E** | *(Retirado el 2026-09-27 junto con el paquete.)* El paquete portable, con autenticación de desarrollo, queda expuesto en una red |"""),
])

patch(r"docs\PENDING-ITEMS.md", [
("""  - Git repository with backup scripts, and a portable package to test on other machines.""",
 """  - Git repository with backup scripts. The portable package that existed from 2026-09-14 was removed on 2026-09-27 by decision: the application runs from the code, with command lines for cmd and PowerShell."""),
("""| SEC-22 | Portable package uses development authentication | Security | P1 | Everyone who distributes it | Accepted risk (with rules) |""",
 """| SEC-22 | Portable package uses development authentication | Security | P1 | Everyone who distributes it | Closed (2026-09-27): package removed |"""),
("""| OP-05 | Portable package distribution checklist | Operations | P2 | Whoever distributes it | Open |""",
 """| OP-05 | Portable package distribution checklist | Operations | P2 | Whoever distributes it | Closed (2026-09-27): package removed |"""),
("""| TD-12 | Lock files and runtime-specific publish | Supply chain | P3 | Development | Mitigated |""",
 """| TD-12 | Lock files and runtime-specific publish | Supply chain | P3 | Development | Moot since 2026-09-27 (portable build removed) |"""),
("""    - the latest portable `.zip`, about 93 MB;
""", ""),
("""### SEC-22 — The portable package uses development authentication

- **Priority:** P1 · **Owner:** everyone who distributes the package · **Status:** Accepted risk with rules""",
"""### SEC-22 — The portable package uses development authentication

- **Closed on 2026-09-27:** the portable package and `tools/portable` were removed. The same rule still applies to running from the code: the development scheme only starts in `Development`, and the commands bind to `localhost`. The text below is kept for traceability.
- **Priority:** P1 · **Owner:** everyone who distributes the package · **Status:** Closed (was: accepted risk with rules)"""),
("""they can also target a running portable package with `MODSCORE_E2E_BASE_URL`.""",
 """they can also target an already running site with `MODSCORE_E2E_BASE_URL`."""),
("""### TD-12 — Lock files and runtime-specific publish

- **Priority:** P3 · **Owner:** development · **Status:** Mitigated""",
"""### TD-12 — Lock files and runtime-specific publish

- **Priority:** P3 · **Owner:** development · **Status:** Moot since 2026-09-27: the portable build that published with `-r win-x64` was removed"""),
("""  4. Watch repository size. Each portable `.zip` adds about 93 MB to the history for good, and GitHub rejects files over 100 MB. Keep only the latest `.zip` in the tree; move to GitHub Releases or Git LFS if builds become frequent.""",
 """  4. Watch repository size. The portable `.zip` files versioned until 2026-09-27 stay in the history (about 93 MB each). If the size becomes a problem, purge them with a history rewrite (`git filter-repo`), coordinated with everyone who has a clone. GitHub rejects files over 100 MB."""),
("""### OP-05 — Portable package distribution checklist
- **Priority:** P2 · **Owner:** whoever distributes it · **Status:** Open""",
"""### OP-05 — Portable package distribution checklist
- **Closed on 2026-09-27:** the portable package was removed; the application runs from the code ([guide](guia-localhost.md)).
- **Priority:** P2 · **Owner:** whoever distributes it · **Status:** Closed"""),
("""| Portable executable package | Self-contained single-file build for System and Channel with the SPA, launcher with local-only binding and per-run token | `tools/portable/` |""",
 """| Portable executable package | Self-contained single-file build for System and Channel with the SPA, launcher with local-only binding and per-run token. **Removed on 2026-09-27** | `tools/portable/` (deleted) |"""),
("""| SPA end-to-end against the portable package | 49 | With `start.ps1 -NoBrowser` running, from `src/web`: `MODSCORE_E2E_BASE_URL=http://localhost:5100 npx playwright test` |
""",
"""| SPA end-to-end against an already running site | Same | With the site running (for example the Channel serving `src/web/dist`), from `src/web`: `MODSCORE_E2E_BASE_URL=http://localhost:5100 npx playwright test` |
"""),
])

patch("HANDOFF.md", [
("""Guarda todo lo creado —el `.zip` portable, el estado local de `data/`,""",
 """Guarda todo lo creado —el estado local de `data/`,"""),
("""| Todo en el repositorio | Paquete portable, estado local de prueba, scripts de las sesiones, copia del plan, `.claude/launch.json` | `artifacts/`, `data/`, `tools/sesiones/`, `docs/plan/` |""",
 """| Todo en el repositorio | Estado local de prueba, línea de tiempo dummy, scripts de las sesiones, copia del plan, `.claude/launch.json` | `data/`, `tools/sesiones/`, `docs/plan/` |"""),
("""| Paquete portable | Ejecutables de un solo archivo con la SPA; lanzador local | `tools/portable/` |
""", ""),
("""| Pruebas contra el paquete portable | Con `start.ps1 -NoBrowser` corriendo, desde `src/web`: `MODSCORE_E2E_BASE_URL=http://localhost:5100 npx playwright test` |""",
 """| Pruebas contra un sitio ya arrancado | Con la Channel sirviendo `src/web/dist` en el 5100, desde `src/web`: `MODSCORE_E2E_BASE_URL=http://localhost:5100 npx playwright test` |"""),
("""| Paquete portable | `powershell -ExecutionPolicy Bypass -File tools/portable/build-portable.ps1` |
""", ""),
("""- **El esquema de desarrollo** solo arranca en `Development`. El paquete portable lo usa, y
  por eso escucha solo en `127.0.0.1`.""",
"""- **El esquema de desarrollo** solo arranca en `Development`, y los comandos de arranque
  escuchan en `localhost`: no sirve para nada fuera de ese equipo."""),
("""  bloquean programas sin firmar ese `.exe` no abre, igual que los del paquete portable.
  `-p:UseAppHost=false` hace que ejecute `dotnet.exe` con la DLL. La SPA no necesita Node: la
  capa Channel la sirve si `--webroot` (o `ASPNETCORE_WEBROOT`) apunta a una carpeta con
  `index.html`, y el `.zip` portable trae esa carpeta ya compilada en `channel\\wwwroot`.""",
"""  bloquean programas sin firmar ese `.exe` no abre. `-p:UseAppHost=false` hace que ejecute
  `dotnet.exe` con la DLL. La SPA no necesita Vite: la capa Channel la sirve si `--webroot`
  (o `ASPNETCORE_WEBROOT`) apunta a una carpeta con `index.html`, la que deja
  `npm run build` en `src\\web\\dist` (se puede compilar en otro equipo y copiar)."""),
("""  carpeta de trabajo. El paquete portable ya lo admitía con `-Data File`; ahora su README lo
  pone primero, y el doble clic sigue abriendo Dummy hasta que se decida cambiarlo.""",
"""  carpeta de trabajo."""),
])

patch("PLAN.md", [
("""- **Repositorio git** privado en GitHub (`main`, primer push el 2026-09-15). Guarda todo lo
  creado —incluidos el paquete portable, el estado local de `data/`, los scripts de las
  sesiones y la copia del plan— salvo las capturas del BRD y las imágenes.""",
"""- **Repositorio git** privado en GitHub (`main`, primer push el 2026-09-15). Guarda todo lo
  creado —el estado local de `data/`, los scripts de las sesiones y la copia del plan— salvo
  las capturas del BRD y las imágenes."""),
("""- **Paquete portable** (`tools/portable/`): ejecutables de un solo archivo con la SPA
  dentro. Escucha solo en `127.0.0.1`, con un token de servicio aleatorio por arranque.
  **No es un despliegue**: usa la autenticación de desarrollo.""",
"""- **Paquete portable** (`tools/portable/`): ejecutables de un solo archivo con la SPA
  dentro, solo en `127.0.0.1`. **Retirado el 2026-09-27** por decisión: la aplicación se
  levanta desde el código con comandos para cmd y PowerShell ([guía](docs/guia-localhost.md))."""),
("""├─ artifacts/                        # último paquete portable (.zip)
""", ""),
("""│  ├─ portable/                      # paquete portable de prueba para otros equipos
""", ""),
])
