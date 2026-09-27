from patchlib import patch

SYS_EXE = r".\Modernization.SystemApi.exe --environment Development --urls http://127.0.0.1:5101"
FILES_PKG = r" --DataSource:Provider=File --DataSource:Directory=C:\modscore\data"

patch(r"tools\portable\README.md", [
# Seccion 2
("""El script levanta las dos capas, genera un token de servicio nuevo en cada arranque y abre
el navegador en <http://localhost:5100/>. `Sign in` entra con el usuario de desarrollo, que
tiene todos los permisos. **Para detener: Enter en esa misma ventana.**

**cmd:**

```bat
cd /d C:\\modscore
.\\start.cmd
```

**PowerShell:**

```powershell
Set-Location C:\\modscore
powershell -NoProfile -ExecutionPolicy Bypass -File .\\start.ps1
```

`-ExecutionPolicy Bypass` vale solo para ese proceso: no cambia la política del equipo.
También funciona el doble clic en `start.cmd` desde el Explorador.""",
"""El script levanta las dos capas, genera un token de servicio nuevo en cada arranque y abre
el navegador en <http://localhost:5100/>. `Sign in` entra con el usuario de desarrollo, que
tiene todos los permisos. **Para detener: Enter en esa misma ventana.**

Con `-Data File`, la pantalla **Data** acepta cargas y lo cargado queda en `C:\\modscore\\data`.

**cmd:**

```bat
cd /d C:\\modscore
.\\start.cmd -Data File
```

**PowerShell:**

```powershell
Set-Location C:\\modscore
powershell -NoProfile -ExecutionPolicy Bypass -File .\\start.ps1 -Data File
```

`-ExecutionPolicy Bypass` vale solo para ese proceso: no cambia la política del equipo.

**Sin `-Data File`** —o con doble clic en `start.cmd` desde el Explorador— arranca con 250
aplicaciones generadas en memoria: sirve para mirar la aplicación al instante, pero **Data no
acepta cargas** y avisa *The active adapter is 'Dummy'*.

La primera vez con `-Data File` no hay datos: cada pantalla lo avisa y lleva a **Data**. Ahí,
en **Upload files**, elegir los ocho CSV de `C:\\modscore\\samples` (o los propios) y
**Upload and recalculate**."""),
("""| *(ninguna)* | 250 aplicaciones generadas en memoria (se pierden al detener) |""",
"""| *(ninguna)* | 250 aplicaciones generadas en memoria (se pierden al detener). **Data no acepta cargas** |"""),
# Seccion 3, ventana 1
("""**Ventana 1 — capa System.** En cmd:

```bat
cd /d C:\\modscore\\system
""" + SYS_EXE + """
```

En PowerShell:

```powershell
Set-Location C:\\modscore\\system
""" + SYS_EXE + """
```
""",
"""**Ventana 1 — capa System**, con carga de archivos. En cmd:

```bat
if not exist C:\\modscore\\data mkdir C:\\modscore\\data
cd /d C:\\modscore\\system
""" + SYS_EXE + FILES_PKG + """
```

En PowerShell:

```powershell
New-Item -ItemType Directory -Force C:\\modscore\\data | Out-Null
Set-Location C:\\modscore\\system
""" + SYS_EXE + FILES_PKG + """
```
"""),
("""| `--SystemApi:BaseUrl=http://127.0.0.1:5101` | Dónde encuentra la Channel a la System |

### Con datos de archivos en lugar de generados

Solo cambia la **ventana 1**: dos argumentos más. La carpeta `data` guarda las cargas y las
reglas publicadas; se crea una vez.

En cmd:

```bat
if not exist C:\\modscore\\data mkdir C:\\modscore\\data
cd /d C:\\modscore\\system
""" + SYS_EXE + FILES_PKG + """
```

En PowerShell:

```powershell
New-Item -ItemType Directory -Force C:\\modscore\\data | Out-Null
Set-Location C:\\modscore\\system
""" + SYS_EXE + FILES_PKG + """
```

Después, en el sitio, **Data → Upload files**: elegir los ocho CSV de `C:\\modscore\\samples` y
**Upload and recalculate**.""",
"""| `--SystemApi:BaseUrl=http://127.0.0.1:5101` | Dónde encuentra la Channel a la System |
| `--DataSource:Provider=File` | Carga de archivos: la pantalla **Data** acepta los CSV |
| `--DataSource:Directory=C:\\modscore\\data` | Dónde quedan las cargas y las reglas publicadas. Ruta absoluta |

La primera vez no hay datos: en el sitio, **Data → Upload files**, elegir los ocho CSV de
`C:\\modscore\\samples` y **Upload and recalculate**.

### Con datos generados, sin cargas

Solo cambia la **ventana 1**: sin los dos argumentos `--DataSource:...`. Arranca con 250
aplicaciones inventadas en memoria, que se pierden al detener, y **Data no acepta cargas**.

```bat
cd /d C:\\modscore\\system
""" + SYS_EXE + """
```"""),
# Seccion 5
("""dotnet run --no-build --project src\\system\\Modernization.SystemApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5101
```""",
"""dotnet run --no-build --project src\\system\\Modernization.SystemApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5101 --DataSource:Provider=File --DataSource:Directory=C:\\modscore1\\data\\local
```"""),
("""`.exe`. `--webroot` le dice a la Channel dónde están los archivos del sitio.""",
"""`.exe`. `--webroot` le dice a la Channel dónde están los archivos del sitio. Los dos
`--DataSource:...` encienden la carga de archivos; lo cargado queda en
`C:\\modscore1\\data\\local`, que git ignora."""),
("""- **Data**: con datos de archivos, subir los ocho CSV de `samples\\`; la tendencia aparece
  desde la segunda carga y marca los cambios de reglas.""",
"""- **Data**: arrancando con `-Data File` (o a mano, con los dos `--DataSource:...`), subir los
  ocho CSV de `samples\\`; la tendencia aparece desde la segunda carga y marca los cambios de
  reglas."""),
])
