from patchlib import patch

patch("HANDOFF.md", [
("""| Levantarlo desde la app de Claude | `.claude/launch.json`: `system`, `channel` y `web`, en ese orden y esperando a que la System responda antes de arrancar la Channel |""",
"""| Levantarlo desde la app de Claude | `.claude/launch.json`: `system`, `channel` y `web`, en ese orden y esperando a que la System responda antes de arrancar la Channel. La `system` corre con el adaptador de archivos sobre `data/local` (ruta relativa a la carpeta del proyecto, `../../../data/local`), así que Data acepta cargas; las pruebas e2e siguen con Dummy |"""),
("""| Levantarlo en otro equipo | Guía paso a paso, para alguien que no conoce el proyecto: [docs/guia-localhost.md](docs/guia-localhost.md). En un equipo que bloquea `.exe` sin firmar, su **camino C**: `dotnet run ... -p:UseAppHost=false -- --environment Development --urls ... --webroot ...`, con la Channel sirviendo el sitio. Todos los comandos pasan el entorno y el puerto **como argumentos**, no como variables: la misma línea sirve en cmd y en PowerShell |""",
"""| Levantarlo en otro equipo | Guía paso a paso, para alguien que no conoce el proyecto: [docs/guia-localhost.md](docs/guia-localhost.md). En un equipo que bloquea `.exe` sin firmar, su **camino C**: `dotnet run ... -p:UseAppHost=false -- --environment Development --urls ... --webroot ...`, con la Channel sirviendo el sitio. Todos los comandos pasan el entorno y el puerto **como argumentos**, no como variables: la misma línea sirve en cmd y en PowerShell. La System arranca con `--DataSource:Provider=File --DataSource:Directory=C:\\modscore1\\data\\local`: la carpeta la ignora git y la primera carga son los ocho CSV de `data/dummy` |"""),
("""- **`"vite" no se reconoce como un comando interno o externo`** es siempre que faltan las""",
"""- **Sin `--DataSource:Provider`, la System arranca con Dummy y Data no acepta cargas**
  (*The active adapter is 'Dummy'*). Los comandos de arranque documentados no lo pasaban, y
  quien usa la aplicación en otro equipo no podía subir archivos (2026-09-21). Desde entonces
  la ventana 1 lleva el adaptador de archivos sobre `data\\local`, con ruta absoluta —`dotnet
  run --project` arranca en la carpeta del proyecto— y excluida de git, porque ahí pueden
  terminar datos reales. `data/history` sigue versionada con dos cargas de la fase 8: no es
  carpeta de trabajo. El paquete portable ya lo admitía con `-Data File`; ahora su README lo
  pone primero, y el doble clic sigue abriendo Dummy hasta que se decida cambiarlo.
- **`"vite" no se reconoce como un comando interno o externo`** es siempre que faltan las"""),
])
