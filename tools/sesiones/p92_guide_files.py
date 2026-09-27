from patchlib import patch

FILES_WIN = r" --DataSource:Provider=File --DataSource:Directory=C:\modscore1\data\local"

patch(r"docs\guia-localhost.md", [
# Camino B, terminal 1
("""**Terminal 1 — capa System.** En Windows, en cmd o en PowerShell:

```bat
dotnet run --project src\\system\\Modernization.SystemApi -- --environment Development --urls http://localhost:5101
```

En macOS o Linux:

```bash
dotnet run --project src/system/Modernization.SystemApi -- --environment Development --urls http://localhost:5101
```

Espera a ver `Now listening on: http://localhost:5101` y `Hosting environment: Development`.""",
"""**Terminal 1 — capa System.** En Windows, en cmd o en PowerShell:

```bat
dotnet run --project src\\system\\Modernization.SystemApi -- --environment Development --urls http://localhost:5101""" + FILES_WIN + """
```

En macOS o Linux:

```bash
dotnet run --project src/system/Modernization.SystemApi -- --environment Development --urls http://localhost:5101 --DataSource:Provider=File --DataSource:Directory="$PWD/data/local"
```

Espera a ver `Now listening on: http://localhost:5101` y `Hosting environment: Development`.

Los dos últimos argumentos encienden la **carga de archivos**: lo que subas en la pantalla
Data queda en esa carpeta y sobrevive al reinicio. En Windows, si el proyecto no está en
`C:\\modscore1`, cambia la ruta por `<carpeta del proyecto>\\data\\local`; tiene que ser
**absoluta** (ver [Cargar archivos](#cargar-archivos))."""),
# Camino B, paso 5
("""Abre <http://localhost:5173> y pulsa **Sign in**. Luego, las mismas cuatro comprobaciones del
camino A:""",
"""Abre <http://localhost:5173> y pulsa **Sign in**. **La primera vez no hay datos**: cada
pantalla lo avisa y te lleva a **Data**. Sube ahí los ocho CSV de `data\\dummy`, como se
explica en [Cargar archivos](#cargar-archivos). Luego, las mismas cuatro comprobaciones del
camino A:"""),
# Camino C, paso 5, ventana 1
("""dotnet run --no-build --project src\\system\\Modernization.SystemApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5101
```""",
"""dotnet run --no-build --project src\\system\\Modernization.SystemApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5101""" + FILES_WIN + """
```"""),
# Camino C, paso 6
("""Abre <http://localhost:5100>, pulsa **Sign in**, y haz las cuatro comprobaciones del""",
"""Abre <http://localhost:5100> y pulsa **Sign in**. La primera vez, sube los ocho CSV de
`data\\dummy` en **Data** ([Cargar archivos](#cargar-archivos)). Luego, las cuatro comprobaciones del"""),
("""| `--webroot` | Le dice a la capa Channel dónde están los archivos del sitio, para servirlos ella misma en el puerto 5100. Por eso no hace falta Node ni una tercera ventana |""",
"""| `--webroot` | Le dice a la capa Channel dónde están los archivos del sitio, para servirlos ella misma en el puerto 5100. Por eso no hace falta Node ni una tercera ventana |
| `--DataSource:...` | Igual que en el camino B: carga de archivos, guardada en `C:\\modscore1\\data\\local` |"""),
# Seccion de archivos
("""## Probar con archivos en vez de datos generados

Por defecto la aplicación inventa 250 aplicaciones en memoria, que se pierden al detenerla.
Para probar la carga de archivos de verdad:

**Camino A:**

```powershell
.\\start.ps1 -Data File
```

**Caminos B y C** (en la terminal de la capa System): dos argumentos más al final de la
línea del `dotnet run`. En Windows, en cmd o en PowerShell:

```bat
dotnet run --project src\\system\\Modernization.SystemApi -- --environment Development --urls http://localhost:5101 --DataSource:Provider=File --DataSource:Directory=C:\\modscore1\\data\\history
```

(en el camino C, con `--no-build` y `-p:UseAppHost=false` antes del `--`, como en su paso 5).
En macOS o Linux:

```bash
dotnet run --project src/system/Modernization.SystemApi -- --environment Development --urls http://localhost:5101 --DataSource:Provider=File --DataSource:Directory="$PWD/data/history"
```

> **La ruta tiene que ser absoluta.** `dotnet run --project` arranca con la carpeta del
> proyecto como raíz, así que una ruta relativa apunta a un directorio que no existe: el
> portafolio aparece vacío, sin ningún error que lo explique. Si el repositorio no está en
> `C:\\modscore1`, cambia esa parte de la ruta.

Luego, en el sitio:

1. Entra en **Data**.
2. Arrastra los ocho CSV (los de `samples\\` en el paquete portable, o los de `data/dummy/`
   en el repositorio). **No hace falta renombrarlos**: cada archivo se reconoce por su
   encabezado.
3. Pulsa publicar. La pantalla dice cuántas filas leyó, cuántas rechazó y por qué.

La primera vez que arranques con archivos y sin ninguna carga, las pantallas dirán que no
hay datos y te llevarán a **Data**. Es lo esperado, no un error.""",
"""## Cargar archivos

La aplicación tiene dos modos de datos, y solo uno acepta cargas:

| Modo | Cómo se arranca | Data acepta cargas |
|---|---|---|
| **Archivos** | Caminos B y C tal como están arriba (`--DataSource:Provider=File --DataSource:Directory=...`); camino A con `.\\start.cmd -Data File` | **Sí**. Cada carga queda como una versión y sobrevive al reinicio |
| **Datos generados** | Camino A con doble clic; caminos B y C sin los dos argumentos `--DataSource:...` | No: 250 aplicaciones inventadas en memoria, que se pierden al detener |

Si Data dice *The active adapter is 'Dummy', which serves generated data*, estás en el
segundo modo: detén la capa System y arráncala con la línea completa.

**Dónde queda lo cargado:** en los caminos B y C, en `C:\\modscore1\\data\\local`, que git
ignora para que ningún dato real llegue al repositorio (las reglas publicadas quedan en
`.rules\\`, dentro de la misma carpeta). En el camino A, en la carpeta `data\\` del paquete.

> **La ruta tiene que ser absoluta.** `dotnet run --project` arranca con la carpeta del
> proyecto como raíz, así que una ruta relativa apunta a un directorio dentro de `src\\`: el
> portafolio aparece vacío, sin ningún error que lo explique. Tampoco uses `data\\history`,
> que está versionada: lo que subas ahí quedaría para el próximo commit.

**La primera carga.** Con la carpeta vacía, las pantallas dirán que no hay datos y te
llevarán a **Data**. Es lo esperado, no un error. Luego:

1. En **Upload files**, arrastra los ocho CSV (los de `samples\\` en el paquete portable, o
   los de `data\\dummy` en el repositorio), o los tuyos. **No hace falta renombrarlos**: cada
   archivo se reconoce por su encabezado. `eai_master.csv` es obligatorio.
2. Pulsa **Upload and recalculate**. La pantalla dice cuántas filas leyó, cuántas rechazó y
   por qué.
3. Con los CSV de ejemplo, Portfolio muestra 250 aplicaciones y la 14381 da **79.67**."""),
# Problemas frecuentes: fila nueva
("""| `dotnet` o `npm` «no se reconoce» | No están instalados o la terminal es anterior a la instalación | Cierra y abre la terminal; si sigue, reinicia el equipo |""",
"""| `dotnet` o `npm` «no se reconoce» | No están instalados o la terminal es anterior a la instalación | Cierra y abre la terminal; si sigue, reinicia el equipo |
| En Data: `The active adapter is 'Dummy', which serves generated data. Uploading files requires the file or database adapter.` | La capa System arrancó sin carga de archivos | Caminos B y C: detén la ventana de la System y copia de nuevo su línea completa, con los dos `--DataSource:...`. Camino A: `.\\start.cmd -Data File` |"""),
# Como dejar el equipo
("""3. Si arrancaste con `-Data File`, ahí dentro está la carpeta `data\\` con lo que hayas
   cargado: se va con la carpeta.""",
"""3. Lo que hayas cargado se va con esa carpeta: en el camino A está en `data\\`, y en los
   caminos B y C en `C:\\modscore1\\data\\local` (si usaste otra ruta, bórrala aparte)."""),
# Checklist
("""- [ ] Portfolio muestra ~250 aplicaciones.""",
"""- [ ] En los caminos B y C, Data acepta la primera carga con los CSV de `data\\dummy`.
- [ ] Portfolio muestra ~250 aplicaciones."""),
])
