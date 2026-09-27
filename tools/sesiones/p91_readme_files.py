from patchlib import patch

patch(".gitignore", [
("""# Secretos locales: nunca al repositorio.""",
"""# Lo que se sube en la pantalla Data al correr en local, y las reglas que se publican ahi:
# pueden ser datos reales del portafolio, asi que nunca entran al repositorio.
data/local/

# Secretos locales: nunca al repositorio."""),
])

patch(r".claude\launch.json", [
("""        "-e", "DataSource__Provider=Dummy"
""",
"""        "-e", "DataSource__Provider=File",
        "-e", "DataSource__Directory=../../../data/local"
"""),
])

SYS_WIN = r"dotnet run --no-build --project src\system\Modernization.SystemApi -- --environment Development --urls http://localhost:5101"
SYS_BASH = "dotnet run --no-build --project src/system/Modernization.SystemApi -- --environment Development --urls http://localhost:5101"
FILES_WIN = r" --DataSource:Provider=File --DataSource:Directory=C:\modscore1\data\local"
FILES_BASH = ' --DataSource:Provider=File --DataSource:Directory="$(pwd -W)/data/local"'

patch("README.md", [
("""- **¿Solo vas a usar la aplicación, no a cambiar la interfaz?** No hace falta Node ni la
  ventana 3: la Channel sirve el sitio ya compilado del paquete portable (ver
  [Variantes](#variantes), «Sin Node»).
""",
"""- **¿Solo vas a usar la aplicación, no a cambiar la interfaz?** No hace falta Node ni la
  ventana 3: la Channel sirve el sitio ya compilado del paquete portable (ver
  [Variantes](#variantes), «Sin Node»).
- **La System arranca con carga de archivos** (`--DataSource:Provider=File`): la pantalla
  **Data** acepta CSV, y lo que subas y las reglas que publiques quedan en
  `C:\\modscore1\\data\\local`. Git ignora esa carpeta porque puede tener datos reales. La
  primera vez está vacía: ver [La primera carga](#la-primera-carga). Sin esos dos argumentos,
  la System inventa 250 aplicaciones en memoria y **Data no acepta cargas**.
"""),
# cmd, ventana 1
("""**Ventana 1 — capa System:**

```bat
cd /d C:\\modscore1
""" + SYS_WIN + """
```

Tiene que aparecer""",
"""**Ventana 1 — capa System:**

```bat
cd /d C:\\modscore1
""" + SYS_WIN + FILES_WIN + """
```

Tiene que aparecer"""),
# PowerShell, ventana 1
("""```powershell
Set-Location C:\\modscore1
""" + SYS_WIN + """
```""",
"""```powershell
Set-Location C:\\modscore1
""" + SYS_WIN + FILES_WIN + """
```"""),
# Git Bash
("""```bash
""" + SYS_BASH + """
```""",
"""```bash
""" + SYS_BASH + FILES_BASH + """
```

`pwd -W` da la ruta como la entiende Windows (`C:/modscore1`); `$PWD` daría `/c/modscore1`,
que `dotnet` no reconoce."""),
# Nueva seccion antes de Variantes
("""### Variantes

Se agregan argumentos al final de la línea de la ventana que corresponde:""",
"""### La primera carga

La carpeta `data\\local` empieza vacía, así que cada pantalla avisa que no hay datos y lleva a
**Data**. Es lo esperado. Para cargar:

1. En <http://localhost:5173/>, **Sign in**, y la pestaña **Data**.
2. En **Upload files**, arrastrar los ocho CSV de `C:\\modscore1\\data\\dummy` (datos de
   ejemplo, 250 aplicaciones) o los propios. No hace falta renombrarlos: cada archivo se
   reconoce por su encabezado. `eai_master.csv` es obligatorio.
3. **Upload and recalculate**. La pantalla dice cuántas filas leyó y cuántas rechazó.

Cada carga queda como una versión en **Upload history** y sobrevive al reinicio. Con los CSV
de ejemplo, la aplicación 14381 tiene que dar **79.67**.

### Variantes

Se agregan o quitan argumentos al final de la línea de la ventana que corresponde:"""),
("""| **Datos de archivos en lugar de generados** | En la ventana 1: `--DataSource:Provider=File --DataSource:Directory=C:\\modscore1\\data\\history` (ver [Correr sobre archivos](#correr-sobre-archivos-en-vez-de-datos-generados)) |""",
"""| **Datos generados, sin cargas** | En la ventana 1, quitar `--DataSource:Provider=File --DataSource:Directory=...`: 250 aplicaciones inventadas en memoria, listas al instante, que se pierden al detener. Data no acepta cargas. Es lo que usan las pruebas e2e |
| **Otra carpeta de datos** | Cambiar la ruta de `--DataSource:Directory`. **Siempre absoluta**: `dotnet run --project` arranca en la carpeta del proyecto, y una ruta relativa terminaría dentro de `src\\system\\...`. Nunca una carpeta versionada como `data\\history`: lo subido quedaría para el próximo commit |"""),
# Si algo falla: fila nueva al principio
("""| Lo que ves | Qué pasa | Qué hacer |
|---|---|---|
| `"vite" no se reconoce""",
"""| Lo que ves | Qué pasa | Qué hacer |
|---|---|---|
| En Data: `The active adapter is 'Dummy', which serves generated data. Uploading files requires the file or database adapter.` | La ventana 1 arrancó sin `--DataSource:Provider=File --DataSource:Directory=...` | Detener la ventana 1 (`Ctrl + C`) y copiar de nuevo su línea completa |
| `"vite" no se reconoce"""),
# Seccion Correr sobre archivos
("""## Correr sobre archivos en vez de datos generados

```bash
DataSource__Provider=File DataSource__Directory="$PWD/data/history" dotnet run --project src/system/Modernization.SystemApi
```

Las rutas van absolutas: `dotnet run --project` arranca con la carpeta del proyecto
como raíz, y una ruta relativa como `./data/history` apunta a un directorio que no
existe —el portafolio aparece sin cargas, sin ningún error—.

Con el adaptador de archivos, la pantalla **Cargas** permite subir los CSV y
recalcular. Cada carga se publica como una versión consultable, y la evolución del
score aparece en esa misma pantalla.

En el primer arranque no hay ninguna carga: las pantallas lo dicen y llevan a
**Cargas**, que es la única que funciona sin datos. Para arrancar necesitas al menos una carga. La más simple: genera los dummy,
cópialos a `data/history/<id>/` y escribe su `manifest.json` — o levanta el sistema
con el adaptador Dummy, exporta, y sube los archivos por la interfaz.""",
"""## Correr sobre archivos en vez de datos generados

Es como arrancan los comandos de [Levantar el stack](#levantar-el-stack): la ventana 1 lleva
`--DataSource:Provider=File --DataSource:Directory=C:\\modscore1\\data\\local`.

Las rutas van absolutas: `dotnet run --project` arranca con la carpeta del proyecto
como raíz, y una ruta relativa como `.\\data\\local` apunta a un directorio que no
existe —el portafolio aparece sin cargas, sin ningún error—.

Con el adaptador de archivos, la pantalla **Data** permite subir los CSV y
recalcular. Cada carga se publica como una versión consultable, y la evolución del
score aparece en esa misma pantalla. Las reglas publicadas se guardan en `rules\\`, dentro
de la misma carpeta.

En el primer arranque no hay ninguna carga: las pantallas lo dicen y llevan a
**Data**, que es la única que funciona sin datos. Los ocho CSV de `data\\dummy` sirven para la
primera (ver [La primera carga](#la-primera-carga)).

`data\\history` tiene dos cargas versionadas en el repositorio, de la verificación de la fase 8.
Sirven para mirar la evolución del score sin cargar nada, pero no como carpeta de trabajo:
lo que se suba ahí quedaría para el próximo commit."""),
])
