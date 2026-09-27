from pathlib import Path

p = Path(r"C:\Users\kmiro\Downloads\mod score\docs\guia-localhost.md")
t = p.read_text(encoding="utf-8", newline="")
nl = "\r\n" if "\r\n" in t else "\n"
t = t.replace(nl, "\n")


def rep(a, b, count=1):
    global t
    assert t.count(a) == count, (t.count(a), a[:80])
    t = t.replace(a, b)


# 1. Quitar el camino A (paquete portable) completo.
start = t.index("## Camino A · el paquete portable (Windows, sin instalar nada)")
end = t.index("## Camino B · desde el código (Windows, macOS o Linux)")
t = t[:start] + t[end:]

# 2. Renombrar B -> A y C -> B, con un marcador intermedio.
t = t.replace("caminos B y C", "los dos caminos").replace("Caminos B y C", "Los dos caminos")
t = t.replace("Camino B", "Camino @A@").replace("camino B", "camino @A@")
t = t.replace("Camino C", "Camino B").replace("camino C", "camino B")
t = t.replace("@A@", "A")
t = t.replace("los los dos caminos", "los dos caminos").replace("En los los dos", "En los dos")

# 3. Elegir camino.
rep("""| **Sitio (SPA)** | Lo que ves en pantalla | `5173` en el camino A; en A y C lo sirve la capa Channel |""",
    """| **Sitio (SPA)** | Lo que ves en pantalla | `5173` en el camino A; en el camino B lo sirve la capa Channel |""") if "en A y C" in t else None
t = t.replace("`5173` en el camino A; en A y C lo sirve la capa Channel", "`5173` en el camino A; en el camino B lo sirve la capa Channel")

start = t.index("Hay **tres caminos**. Elige uno:")
end = t.index("---\n\n## Camino A · desde el código")
t = t[:start] + """Hay **dos caminos**, los dos desde el código y con comandos para `cmd` y PowerShell. Elige uno:

| | Camino A · desde el código | Camino B · sin permiso para ejecutar `.exe` |
|---|---|---|
| Para quién | Cualquiera que va a **usar o modificar** la aplicación | Un equipo de empresa que **bloquea programas no firmados** |
| Sistema | Windows, macOS o Linux | Windows, desde `cmd` o PowerShell |
| Hay que instalar | .NET SDK 10 y Node.js | .NET SDK 10 (Node solo para compilar el sitio una vez, en este equipo o en otro) |
| Qué se ejecuta | `dotnet` y `node` | **Solo `dotnet.exe`**, firmado por Microsoft |
| Ventanas abiertas | Tres | Dos |
| Cuánto toma | 20 minutos la primera vez | 15 minutos la primera vez |
| Se abre en | `http://localhost:5173` | `http://localhost:5100` |

Si dudas, empieza por el **camino A**. Si al arrancar Windows dice que un programa está
**bloqueado por el administrador** o que **no tienes permiso**, ve al **camino B**.

""" + t[end:]

# 4. Paso 5 del camino A: las comprobaciones completas.
rep("""Abre <http://localhost:5173> y pulsa **Sign in**. **La primera vez no hay datos**: cada
pantalla lo avisa y te lleva a **Data**. Sube ahí los ocho CSV de `data\\dummy`, como se
explica en [Cargar archivos](#cargar-archivos). Luego, las mismas cuatro comprobaciones del
camino A:

1. Portfolio con unas 250 aplicaciones.
2. `http://localhost:5173/applications/14381/calculation` da **79.67**.
3. **Insights** muestra las seis preguntas.
4. **Dashboard** muestra cinco indicadores y seis paneles.""",
"""Abre <http://localhost:5173> y pulsa **Sign in**. No pide usuario ni contraseña: el esquema
de desarrollo te entra con todos los permisos.

**La primera vez no hay datos**: cada pantalla lo avisa y te lleva a **Data**. Hay dos
formas de cargar, explicadas en [Cargar archivos](#cargar-archivos): subir los ocho CSV de
`data\\dummy`, o copiar antes de arrancar los cuatro trimestres de `data\\timeline` para ver
la evolución en el tiempo. Luego, cinco comprobaciones de treinta segundos:

1. Al entrar se abre el **Dashboard**. En la pestaña **Portfolio** tiene que haber una tabla
   con unas 250 aplicaciones.
2. Abre `http://localhost:5173/applications/14381/calculation`. El score tiene que ser
   exactamente **79.67**: es el ejemplo trabajado del documento de negocio, y si ese número
   sale, el cálculo está bien de punta a punta.
3. **Insights** muestra las seis preguntas del portafolio.
4. **Dashboard** muestra cinco indicadores y seis paneles; un clic en un indicador abre la
   lista de aplicaciones que cuenta.
5. **Code** avisa en ámbar que los repositorios son de demostración, muestra la proyección a
   uno, dos y tres años y el plan de mejora. Con los cuatro trimestres cargados, además
   compara el score, el Currency Score y el riesgo carga por carga."""),

rep("""Deben terminar en verde: 737 pruebas .NET y 47 de la interfaz.""",
    """Deben terminar en verde: 847 pruebas .NET y 66 de la interfaz.""")

# 5. Camino B (antes C) sin el paquete.
rep("""Para equipos de empresa donde Windows no deja ejecutar programas que no estén firmados: los
`.exe` del paquete portable no abren, y tampoco los que crea `dotnet run` por su cuenta.

La idea es que **el único programa que se ejecuta sea `dotnet.exe`**, que es de Microsoft y
está firmado. El código de la aplicación lo carga `dotnet` como biblioteca, y el sitio lo
sirve la propia capa Channel desde archivos HTML, JavaScript y CSS, que no se ejecutan. No
hace falta Node.""",
"""Para equipos de empresa donde Windows no deja ejecutar programas que no estén firmados: los
`.exe` que crea `dotnet run` por su cuenta no abren.

La idea es que **el único programa que se ejecuta sea `dotnet.exe`**, que es de Microsoft y
está firmado. El código de la aplicación lo carga `dotnet` como biblioteca, y el sitio lo
sirve la propia capa Channel desde archivos HTML, JavaScript y CSS ya compilados, que no se
ejecutan. Node hace falta una sola vez, para compilar el sitio, y puede hacerse en otro
equipo.""")

start = t.index("### Paso 3 · consigue los archivos del sitio, sin compilar nada")
end = t.index("### Paso 4 · compila una sola vez")
t = t[:start] + """### Paso 3 · compila el sitio una sola vez

El sitio compilado es una carpeta de archivos estáticos, `src\\web\\dist`. Se genera con Node,
**en este equipo o en cualquier otro** que tenga Node 22.12 o superior y acceso al registro
de npm.

En `cmd`:

```bat
cd /d C:\\modscore1
npm --prefix src\\web ci --ignore-scripts
npm --prefix src\\web run build
dir src\\web\\dist\\index.html
```

En PowerShell:

```powershell
Set-Location C:\\modscore1
npm.cmd --prefix src\\web ci --ignore-scripts
npm.cmd --prefix src\\web run build
Test-Path src\\web\\dist\\index.html
```

El último comando tiene que encontrar `index.html` (en PowerShell, responder `True`). Si lo
compilaste en otro equipo, copia la carpeta `dist` completa a `C:\\modscore1\\src\\web\\dist`
en este. La compilación ejecuta un `esbuild.exe` que viene con las dependencias: si la misma
política que bloquea los `.exe` lo bloquea, compílalo en otro equipo.

""" + t[end:]

rep("""dotnet run --no-build --project src\\channel\\Modernization.ChannelApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5100 --webroot C:\\modscore-sitio\\wwwroot""",
    """dotnet run --no-build --project src\\channel\\Modernization.ChannelApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5100 --webroot C:\\modscore1\\src\\web\\dist""")
rep("""> **no es un error**: en local todo va por HTTP y la aplicación solo avisa que no tiene a
> dónde redirigir. Desde el 2026-09-17 ya no se muestra al correr desde el código; el
> paquete portable anterior todavía lo imprime.""",
"""> **no es un error**: en local todo va por HTTP y la aplicación solo avisa que no tiene a
> dónde redirigir.""")
rep("""Abre <http://localhost:5100> y pulsa **Sign in**. La primera vez, sube los ocho CSV de
`data\\dummy` en **Data** ([Cargar archivos](#cargar-archivos)). Luego, las cuatro comprobaciones del
[paso 5 del camino A](#paso-5--comprueba-que-quedó-bien) cambiando nada más que el puerto:
Portfolio con ~250 aplicaciones, `/applications/14381/calculation` con **79.67**,
**Insights** con las seis preguntas y **Dashboard** con cinco indicadores y seis paneles.""",
"""Abre <http://localhost:5100> y pulsa **Sign in**. La primera vez, carga los datos como se
explica en [Cargar archivos](#cargar-archivos). Luego, las cinco comprobaciones del
[paso 5 del camino A](#paso-5--entra-y-comprueba) cambiando nada más que el puerto: 5100 en
lugar de 5173.""")
start = t.index("### Si prefieres compilar el sitio en vez de sacarlo del `.zip`")
end = t.index("---\n\n## Cargar archivos")
t = t[:start] + t[end:]

# 6. Cargar archivos.
rep("""| **Archivos** | Los dos caminos tal como están arriba (`--DataSource:Provider=File --DataSource:Directory=...`); camino A con `.\\start.cmd -Data File` | **Sí**. Cada carga queda como una versión y sobrevive al reinicio |
| **Datos generados** | Camino A con doble clic; los dos caminos sin los dos argumentos `--DataSource:...` | No: 250 aplicaciones inventadas en memoria, que se pierden al detener |""",
"""| **Archivos** | Los dos caminos tal como están arriba (`--DataSource:Provider=File --DataSource:Directory=...`) | **Sí**. Cada carga queda como una versión y sobrevive al reinicio |
| **Datos generados** | Los dos caminos sin los dos argumentos `--DataSource:...` | No: 250 aplicaciones inventadas en memoria, con sus cuatro trimestres, que se pierden al detener |""")
rep("""**Dónde queda lo cargado:** en los dos caminos, en `C:\\modscore1\\data\\local`, que git
ignora para que ningún dato real llegue al repositorio (las reglas publicadas quedan en
`.rules\\`, dentro de la misma carpeta). En el camino A, en la carpeta `data\\` del paquete.""",
"""**Dónde queda lo cargado:** en `C:\\modscore1\\data\\local`, que git ignora para que ningún
dato real llegue al repositorio (las reglas publicadas quedan en `.rules\\`, dentro de la
misma carpeta).""")
rep("""1. En **Upload files**, arrastra los ocho CSV (los de `samples\\` en el paquete portable, o
   los de `data\\dummy` en el repositorio), o los tuyos. **No hace falta renombrarlos**: cada
   archivo se reconoce por su encabezado. `eai_master.csv` es obligatorio.
2. Pulsa **Upload and recalculate**. La pantalla dice cuántas filas leyó, cuántas rechazó y
   por qué.
3. Con los CSV de ejemplo, Portfolio muestra 250 aplicaciones y la 14381 da **79.67**.""",
"""1. En **Upload files**, arrastra los ocho CSV de `data\\dummy`, o los tuyos. **No hace falta
   renombrarlos**: cada archivo se reconoce por su encabezado. `eai_master.csv` es
   obligatorio.
2. Pulsa **Upload and recalculate**. La pantalla dice cuántas filas leyó, cuántas rechazó y
   por qué.
3. Con los CSV de ejemplo, Portfolio muestra 250 aplicaciones y la 14381 da **79.67**.

**Para comparar en el tiempo: los cuatro trimestres de ejemplo.** `data\\timeline` trae el
mismo portafolio en cuatro cargas —Q4 2025, Q1, Q2 y Q3 2026—, con aplicaciones que se
modernizan y otras que envejecen. Cópialas a `data\\local` **antes de arrancar la capa
System** (si ya estaba corriendo, detenla con Ctrl + C y vuelve a arrancarla).

En `cmd`:

```bat
cd /d C:\\modscore1
xcopy /E /I /Y data\\timeline data\\local
```

En PowerShell:

```powershell
Set-Location C:\\modscore1
New-Item -ItemType Directory -Force data\\local | Out-Null
Copy-Item -Recurse -Force data\\timeline\\* data\\local
```

Después, en **Data → Upload history** aparecen las cuatro cargas, y la pantalla **Code**
compara el Modernization Score, el Currency Score y el Risk Score de una a otra; el detalle
de cada aplicación muestra su propia evolución.""")

# 7. Usuario limitado, con argumentos.
rep("""Por defecto entras como administrador de todo. Para ver la aplicación como la vería alguien
que solo puede consultar un LOB, arranca la **capa Channel** así:

```bash
Authentication__Development__Roles__0=portfolio.read Authentication__Development__Scopes__0=lob:Individual dotnet run --project src/channel/Modernization.ChannelApi
```""",
"""Por defecto entras como administrador de todo. Para ver la aplicación como la vería alguien
que solo puede consultar un LOB, agrega dos argumentos al final de la línea de la **capa
Channel** (la misma línea en cmd y en PowerShell):

```bat
dotnet run --no-build --project src\\channel\\Modernization.ChannelApi -- --environment Development --urls http://localhost:5100 --Authentication:Development:Roles:0=portfolio.read --Authentication:Development:Scopes:0=lob:Individual
```""")

# 8. Problemas frecuentes.
rep("""| La capa System arrancó sin carga de archivos | Los dos caminos: detén la ventana de la System y copia de nuevo su línea completa, con los dos `--DataSource:...`. Camino A: `.\\start.cmd -Data File` |""",
    """| La capa System arrancó sin carga de archivos | Detén la ventana de la System y copia de nuevo su línea completa, con los dos `--DataSource:...` |""")
for row in [
    """| La consola se cierra sola al instante | Doble clic sobre `start.ps1` en vez de `start.cmd` | Usa `start.cmd`, o abre PowerShell y ejecuta `.\\start.ps1` |\n""",
    """| `"start.cmd" no se reconoce como un comando interno o externo` (o lo mismo con un `.exe`), aunque el archivo está en esa carpeta | El equipo tiene la política `NoDefaultCurrentDirectoryInExePath`: `cmd` no busca programas en la carpeta actual | Escribe `.\\start.cmd` (o `.\\Modernization.SystemApi.exe`) |\n""",
    """| `No se puede cargar el archivo start.ps1 ... deshabilitada la ejecución de scripts` | Política de ejecución de PowerShell | `powershell -ExecutionPolicy Bypass -File .\\start.ps1` |\n""",
    """| «Windows protegió su PC» | SmartScreen: los binarios no están firmados | Más información → Ejecutar de todas formas |\n""",
    """| Al ejecutar un `.exe` a mano dice que no lo encuentra | Windows no busca en la carpeta actual | Escribe `.\\Modernization.SystemApi.exe`, y hazlo **desde la carpeta de esa capa** |\n""",
]:
    rep(row, "")
rep("""| «Este programa está bloqueado por una directiva de grupo», «el administrador del sistema bloqueó esta aplicación» o «acceso denegado» al abrir `start.cmd` o un `.exe` | La empresa no deja ejecutar programas sin firmar desde carpetas de usuario | Si el `.zip` llegó por internet o por correo, primero: clic derecho sobre el `.zip` → Propiedades → **Desbloquear**, y descomprímelo otra vez. Si sigue bloqueado, usa el **camino B** |""",
    """| «Este programa está bloqueado por una directiva de grupo», «el administrador del sistema bloqueó esta aplicación» o «acceso denegado» al arrancar una capa | La empresa no deja ejecutar programas sin firmar desde carpetas de usuario | Usa el **camino B**: solo corre `dotnet.exe` |""")
rep("""| Camino B: `http://localhost:5100` responde 404 o una página vacía | `--webroot` no apunta a una carpeta con `index.html` | `dir C:\\modscore-sitio\\wwwroot\\index.html` tiene que encontrar el archivo; corrige la ruta y reinicia la ventana 2 |""",
    """| Camino B: `http://localhost:5100` responde 404 o una página vacía | `--webroot` no apunta a una carpeta con `index.html` | `dir C:\\modscore1\\src\\web\\dist\\index.html` tiene que encontrar el archivo; si no, repite el paso 3 del camino B y reinicia la ventana 2 |""")
rep("""| `Failed to bind to address ... address already in use` | Otro programa ocupa 5100, 5101 o 5173 | Camino A: `.\\start.ps1 -Port 5200`. Camino A: cierra lo que use el puerto (ver fila siguiente) |""",
    """| `Failed to bind to address ... address already in use` | Otro programa ocupa 5100, 5101 o 5173 | Cierra lo que use el puerto (ver fila siguiente), o cambia el número en `--urls` |""")
rep("""| La ventana negra se cerró y los puertos siguen ocupados | Los procesos quedaron vivos | Camino A: `taskkill /IM Modernization.SystemApi.exe /F` y lo mismo con `Modernization.ChannelApi.exe`. Los dos caminos, donde el proceso se llama `dotnet.exe`: `netstat -ano \\| findstr :5100` da el número de proceso, y `taskkill /PID <número> /F` lo cierra |""",
    """| Se cerró una ventana y el puerto sigue ocupado | El proceso quedó vivo | `netstat -ano \\| findstr :5100` da el número de proceso al final de la línea, y `taskkill /PID <número> /F` lo cierra (en PowerShell, `Get-NetTCPConnection -LocalPort 5100 -State Listen` y `Stop-Process -Id <número>`) |""")
rep("""Si no vas a cambiar la interfaz, puedes saltarte Node: la Channel sirve el sitio ya compilado con `--webroot` (camino B, paso 5) |""",
    """Si no vas a cambiar la interfaz, la Channel puede servir el sitio compilado con `--webroot` (camino B) |""")
rep("""| Pide a soporte el proxy o el registro interno de npm. Mientras tanto, usa la Channel sirviendo el sitio con `--webroot` (camino B), que no necesita Node |""",
    """| Pide a soporte el proxy o el registro interno de npm. Mientras tanto, compila el sitio en un equipo con acceso, copia `src\\web\\dist` y sigue el camino B |""")

# 9. Dejar el equipo y checklist.
rep("""1. Detén la aplicación (Enter en la consola, o Ctrl + C en las tres terminales).
2. Borra la carpeta donde descomprimiste el paquete (camino A) o donde clonaste el
   repositorio (camino A).
3. Lo que hayas cargado se va con esa carpeta: en el camino A está en `data\\`, y en los
   los dos caminos en `C:\\modscore1\\data\\local` (si usaste otra ruta, bórrala aparte).

No queda nada instalado ni registrado en el sistema: el paquete portable no toca el
registro de Windows ni instala servicios.""",
"""1. Detén la aplicación: Ctrl + C en cada ventana.
2. Borra la carpeta donde clonaste el repositorio.
3. Lo que hayas cargado se va con esa carpeta, en `C:\\modscore1\\data\\local` (si usaste otra
   ruta, bórrala aparte).

La aplicación no instala servicios ni toca el registro de Windows. Lo único instalado son el
.NET SDK y Node, que se desinstalan como cualquier programa.""")
rep("""- [ ] Las tres piezas arriba (o las dos del paquete portable).""",
    """- [ ] Las tres piezas arriba (o las dos del camino B).""")
rep("""- [ ] En los dos caminos, Data acepta la primera carga con los CSV de `data\\dummy`.""",
    """- [ ] Data acepta la primera carga con los CSV de `data\\dummy`, o muestra los cuatro
      trimestres copiados de `data\\timeline`.""")
rep("""- [ ] **Code** avisa en ámbar que los repositorios son de demostración y muestra las fechas de
      fin de vida de endoflife.date (los dos caminos; en el paquete portable, desde el generado
      el 2026-09-26).""",
"""- [ ] **Code** avisa en ámbar que los repositorios son de demostración y muestra la
      proyección a uno, dos y tres años, el plan de mejora y, con los cuatro trimestres, la
      evolución por carga.""")
rep("""- [tools/portable/README.md](../tools/portable/README.md) — detalle del paquete portable y
  cómo generar uno nuevo.
""", "")

p.write_text(t.replace("\n", nl), encoding="utf-8", newline="")
print("ok guide")
