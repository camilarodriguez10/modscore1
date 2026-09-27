from patchlib import patch

patch("HANDOFF.md", [
("""- **Un bloque de `cmd` pegado en PowerShell no define variables de entorno.**""",
"""- **`"vite" no se reconoce como un comando interno o externo`** es siempre que faltan las
  dependencias del sitio: `npm run dev` busca `vite` en `src/web/node_modules/.bin`. Tres
  formas de llegar ahí, todas vistas: no correr la instalación, correrla en PowerShell con
  `npm` (resuelve a `npm.ps1` y la política la bloquea) o correrla en otra carpeta. Los
  comandos documentados usan `npm --prefix src\\web ci --ignore-scripts` —exactamente el lock
  auditado, como el CI— y comprueban después que exista `vite.cmd`; en PowerShell, `npm.cmd`.
  Vite 7 pide Node 22.12+ o 20.19+, no «22 o superior». Lo reportó quien usa la aplicación en
  otro equipo el 2026-09-21.
- **Un bloque de `cmd` pegado en PowerShell no define variables de entorno.**"""),
])
