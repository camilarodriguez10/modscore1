from patchlib import patch

patch("README.md", [
("""| Cómo se levanta | Qué hay que tener instalado |
|---|---|
| **Paquete portable** (`.\\start.cmd -Data File` o sus `.exe` a mano) | **Nada**: la variante autocontenida trae su propio .NET. Ver [tools/portable/README.md](tools/portable/README.md) |
| **Desde el código**, con los comandos de abajo | **.NET SDK 10** (<https://dotnet.microsoft.com/download/dotnet/10.0>) y **Node.js 22.12 o superior**, o 20.19+ (<https://nodejs.org>) |
| **Desde el código sin Node** (variante «Sin Node») | Solo el **.NET SDK 10**, más la carpeta `channel\\wwwroot` del paquete portable |""",
"""| Cómo se levanta | Qué hay que tener instalado |
|---|---|
| **Los comandos de abajo**, en tres ventanas | **.NET SDK 10** (<https://dotnet.microsoft.com/download/dotnet/10.0>) y **Node.js 22.12 o superior**, o 20.19+ (<https://nodejs.org>) |
| **Con el sitio compilado**, en dos ventanas (variante de abajo) | **.NET SDK 10**. Node solo hace falta para compilar el sitio una vez, y eso puede hacerse en otro equipo y copiar la carpeta `src\\web\\dist` |"""),
("""- **¿Solo vas a usar la aplicación, no a cambiar la interfaz?** No hace falta Node ni la
  ventana 3: la Channel sirve el sitio ya compilado del paquete portable (ver
  [Variantes](#variantes), «Sin Node»).""",
"""- **¿Solo vas a usar la aplicación, no a cambiar la interfaz?** Se puede prescindir de la
  ventana 3: la Channel sirve el sitio compilado una vez (ver [Variantes](#variantes),
  «Con el sitio compilado»)."""),
("""Cada carga queda como una versión en **Upload history** y sobrevive al reinicio. Con los CSV
de ejemplo, la aplicación 14381 tiene que dar **79.67**.""",
"""Cada carga queda como una versión en **Upload history** y sobrevive al reinicio. Con los CSV
de ejemplo, la aplicación 14381 tiene que dar **79.67**.

### Para comparar en el tiempo: cuatro trimestres de ejemplo

`data\\timeline` trae el mismo portafolio en cuatro cargas trimestrales —Q4 2025, Q1, Q2 y Q3
2026—: las mismas 250 aplicaciones, unas modernizándose y otras envejeciendo. Copiadas a
`data\\local` **antes de arrancar la ventana 1**, la pantalla **Code** compara el score, el
Currency Score y el riesgo carga por carga, y el detalle de cada aplicación muestra su propia
evolución.

En cmd:

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

Si la ventana 1 ya estaba corriendo, hay que detenerla (`Ctrl + C`) y volver a arrancarla. Con
datos generados (sin los dos `--DataSource:...`), los cuatro trimestres ya vienen incluidos.
Los regenera `dotnet run --project tools/Modernization.DummyData -- data/dummy`."""),
("""| **Sin Node: la Channel sirve el sitio** | Se omite la ventana 3 y en la ventana 2 se agrega `--webroot C:\\modscore\\channel\\wwwroot` (la carpeta `wwwroot` del paquete portable). El sitio queda en <http://localhost:5100/> |""",
"""| **Con el sitio compilado** | Una vez, `npm --prefix src\\web run build` (en PowerShell, `npm.cmd`), que deja el sitio en `src\\web\\dist`. Después se omite la ventana 3 y en la ventana 2 se agrega `--webroot C:\\modscore1\\src\\web\\dist`. El sitio queda en <http://localhost:5100/>. La carpeta `dist` se puede compilar en otro equipo y copiar |"""),
("""Ejemplo del camino C completo, ventana 2:

```bat
dotnet run --no-build --project src\\channel\\Modernization.ChannelApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5100 --webroot C:\\modscore\\channel\\wwwroot
```

- **Paquete portable, sin el código:** los comandos para cmd y PowerShell están en
  [tools/portable/README.md](tools/portable/README.md), que viaja dentro del `.zip` como
  `README.txt`.
- **Desde la app de Claude:**""",
"""Ejemplo de la ventana 2 con el sitio compilado y sin ejecutables propios (camino C de la guía):

```bat
dotnet run --no-build --project src\\channel\\Modernization.ChannelApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5100 --webroot C:\\modscore1\\src\\web\\dist
```

- **Desde la app de Claude:**"""),
("""| La red de la empresa no deja llegar al registro de npm | Pedir a soporte el proxy o el registro interno de npm. Mientras tanto, usar la variante **sin Node** |""",
"""| La red de la empresa no deja llegar al registro de npm | Pedir a soporte el proxy o el registro interno de npm. Mientras tanto, compilar el sitio en un equipo con acceso, copiar `src\\web\\dist` y usar la variante **con el sitio compilado** |"""),
("""## Paquete portable para probar en otro equipo

```powershell
powershell -ExecutionPolicy Bypass -File tools/portable/build-portable.ps1
```

Genera en `artifacts/` un `.zip` con las dos capas como ejecutables de un solo archivo y la
SPA dentro, más su SHA-256. El último `.zip` se versiona en el repositorio. En el otro equipo
se descomprime y se ejecuta `start.cmd`. Ver [tools/portable/README.md](tools/portable/README.md)
y, para el paso a paso completo, [docs/guia-localhost.md](docs/guia-localhost.md).

**No es un despliegue:** usa la autenticación de desarrollo, y por eso escucha solo en
`127.0.0.1`.

""", ""),
("""data/dummy/                     los ocho CSV generados""",
 """data/dummy/                     los ocho CSV generados
data/timeline/                  el mismo portafolio en cuatro cargas trimestrales"""),
("""tools/                          datos dummy, prueba de carga, respaldo en git y paquete portable""",
 """tools/                          datos dummy, catálogo de fin de vida, prueba de carga y respaldo en git"""),
# seccion Code
("""- **Las reglas son el área `code` de las definiciones**: 14 parámetros editables en
  *Rules → Portfolio analytics*. El índice no cambia el Modernization Score.""",
"""- **En el tiempo:** *How did score, currency and risk move across uploads?* compara, carga
  por carga, el Modernization Score, el Currency Score de CMT y el Risk Score promedio; *Where
  is obsolescence heading if nothing changes?* proyecta hoy, en uno, dos y tres años con las
  mismas versiones, las mismas alertas y ningún commit más: componentes vencidos, parte del
  código que sigue con soporte e índice de riesgo.
- **Qué cambiar:** *What should change first?* es el plan de mejora —«Upgrade Node.js 14 →
  26», «Migrate AngularJS → Angular 22», «Replace request»—, cada acción una vez con todos
  los repositorios donde aplica, por urgencia y por puntos de riesgo que baja o evita. El
  destino es el ciclo con soporte que más dura, LTS cuando existe. Cada aplicación tiene su
  propia lista, su proyección y su evolución por carga en el detalle.
- **Las reglas son el área `code` de las definiciones**: 15 parámetros editables en
  *Rules → Portfolio analytics*. El índice no cambia el Modernization Score."""),
])
