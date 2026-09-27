# Levantar Modernization Score en otro equipo — paso a paso

Guía para dejar la aplicación funcionando en `localhost` en un equipo que nunca la ha
tenido. No hace falta saber .NET ni React: todo lo que hay que escribir está aquí para
copiar y pegar.

> **Esto no es un despliegue.** La aplicación arranca en modo desarrollo, con un esquema de
> autenticación que **no autentica a nadie**: quien llegue al puerto entra como
> administrador. Por eso escucha solo en `127.0.0.1` (este equipo) y nunca en la red. No la
> uses con datos reales de la organización.

---

## Antes de empezar: qué vas a levantar

Son tres piezas que hablan entre sí. Da igual si no te dicen nada los nombres; lo que
importa es que **todas tienen que estar arriba** para que el sitio funcione.

| Pieza | Qué hace | Puerto |
|---|---|---|
| **Capa System** | Calcula los scores y lee los datos | `5101` |
| **Capa Channel** | Es a la que le habla el navegador; nunca calcula nada | `5100` |
| **Sitio (SPA)** | Lo que ves en pantalla | `5173` en el camino B; en A y C lo sirve la capa Channel |

Hay **tres caminos**. Elige uno:

| | Camino A · paquete portable | Camino B · desde el código | Camino C · sin permiso para ejecutar `.exe` |
|---|---|---|---|
| Para quién | Alguien que solo quiere **usar** la aplicación | Alguien que va a **modificarla** | Un equipo de empresa que **bloquea programas no firmados** |
| Sistema | Windows 10/11 de 64 bits | Windows, macOS o Linux | Windows, desde `cmd` |
| Hay que instalar | **Nada** | .NET 10 y Node.js | Solo .NET 10 |
| Qué se ejecuta | Los `.exe` del paquete | `dotnet` y `node` | **Solo `dotnet.exe`**, firmado por Microsoft |
| Cuánto toma | 5 minutos | 20 minutos la primera vez | 15 minutos la primera vez |
| Se abre en | `http://localhost:5100` | `http://localhost:5173` | `http://localhost:5100` |

Si dudas, empieza por el **camino A**. Si al abrir `start.cmd` Windows dice que el programa
está **bloqueado por el administrador** o que **no tienes permiso**, ve directo al
**camino C**.

---

## Camino A · el paquete portable (Windows, sin instalar nada)

### Paso 1 · consigue el `.zip`

Está en el repositorio, en la carpeta `artifacts/`, con un nombre como
`modscore-portable-win-x64-selfcontained-AAAAMMDD-HHMM.zip` (unos 93 MB). Cópialo al otro
equipo con un pendrive, una carpeta compartida o como prefieras.

**Comprueba que llegó completo** (opcional pero recomendado). Abre PowerShell donde está el
archivo y escribe:

```powershell
Get-FileHash -Algorithm SHA256 .\modscore-portable-*.zip
```

o, en `cmd`:

```bat
for %f in (modscore-portable-*.zip) do certutil -hashfile "%f" SHA256
```

El resultado tiene que coincidir con el SHA-256 que aparece en
[HANDOFF.md](../HANDOFF.md) (fila «Paquete portable»). Si no coincide, el archivo se copió
mal: vuelve a copiarlo.

### Paso 2 · descomprímelo

Clic derecho sobre el `.zip` → **Extraer todo**.

- Elige una carpeta **local y corta**, por ejemplo `C:\modscore`.
- **Evita** el Escritorio dentro de OneDrive, `Descargas` sincronizadas o rutas muy largas:
  la sincronización bloquea archivos y da errores raros al arrancar.
- Tiene que quedar así:

```
C:\modscore\
├─ channel\      (la capa Channel y el sitio)
├─ system\       (la capa System)
├─ samples\      (ocho CSV de ejemplo)
├─ start.cmd     ← esto es lo que se ejecuta
├─ start.ps1
└─ README.txt
```

### Paso 3 · arráncalo

Doble clic en **`start.cmd`**. O, si prefieres escribirlo, en una ventana de `cmd`:

```bat
cd /d C:\modscore
.\start.cmd
```

o en una de PowerShell:

```powershell
Set-Location C:\modscore
powershell -NoProfile -ExecutionPolicy Bypass -File .\start.ps1
```

El `.\` delante no es un adorno: en equipos con la política `NoDefaultCurrentDirectoryInExePath`,
`cmd` no ejecuta nada de la carpeta actual sin él. Si el equipo no deja correr el script pero
sí los `.exe`, el `README.txt` del paquete trae los comandos para arrancar las dos capas a
mano, en cmd y en PowerShell.

Se abre una ventana negra de consola. Verás algo así:

```
  Modernization Score - paquete de PRUEBA
  Autenticacion de desarrollo: no usar con datos reales ni exponer en una red.

Esperando a que las dos capas respondan...

Listo: http://localhost:5100/  (datos: Dummy)
Presione Enter para detener.
```

El navegador se abre solo en `http://localhost:5100/`.

> **Si Windows muestra un aviso azul** («Windows protegió su PC»), es SmartScreen: los
> ejecutables de prueba no están firmados. Pulsa **Más información → Ejecutar de todas
> formas**. Firmar los binarios es trabajo pendiente del proyecto (hallazgo H-17).

### Paso 4 · entra

En la página, pulsa **Sign in**. No pide usuario ni contraseña: el esquema de desarrollo te
entra con todos los permisos.

### Paso 5 · comprueba que quedó bien

Cuatro comprobaciones de treinta segundos:

1. Al entrar se abre el **Dashboard**, la página de inicio. En la pestaña **Portfolio**
   tiene que haber una tabla con unas 250 aplicaciones.
2. Abre `http://localhost:5100/applications/14381/calculation`. El score de esa aplicación
   tiene que ser exactamente **79.67**. Es el ejemplo trabajado del documento de negocio:
   si ese número sale, el cálculo está bien de punta a punta.
3. Abre **Insights** en el menú. Tienen que aparecer las seis preguntas del portafolio, la
   primera con el promedio y la mediana.
4. Vuelve a **Dashboard**, la primera pestaña. Arriba dice *Portfolio Health & Risk
   Intelligence*, debajo hay cinco indicadores y, al bajar, seis paneles numerados. Un clic en
   cualquier indicador abre a la derecha la lista de aplicaciones que cuenta, y
   *How this is calculated*, al pie de cada panel, explica el cálculo con los valores vigentes.

### Paso 6 · para detenerlo

Vuelve a la ventana negra y pulsa **Enter**. Verás `Detenido.`, y con eso se cierran las dos
capas y se borra el token temporal que usan para hablar entre ellas.

> No cierres la ventana con la ✕ si puedes evitarlo: al cerrarla sin más pueden quedar los
> dos procesos vivos. Si pasa, ver [Problemas frecuentes](#problemas-frecuentes).

### Opciones del arranque

Abre PowerShell en `C:\modscore` y usa `start.ps1` en vez del doble clic (en `cmd`, las
mismas opciones van después de `.\start.cmd`, por ejemplo `.\start.cmd -Data File`):

| Comando | Para qué |
|---|---|
| `.\start.ps1` | Igual que el doble clic: 250 aplicaciones generadas en memoria |
| `.\start.ps1 -Data File` | Para **subir archivos** de verdad: la pantalla Data acepta los CSV de `samples\` y lo cargado queda en `data\` |
| `.\start.ps1 -Port 5200` | Si el puerto 5100 está ocupado (la capa System usará el 5201) |
| `.\start.ps1 -NoBrowser` | No abre el navegador solo |

Si PowerShell se queja de la política de ejecución, usa:

```powershell
powershell -ExecutionPolicy Bypass -File .\start.ps1
```

---

## Camino B · desde el código (Windows, macOS o Linux)

Este camino sirve para tocar el código y ver los cambios al vuelo.

### Paso 1 · instala las dos herramientas

| Herramienta | Versión | Dónde |
|---|---|---|
| **.NET SDK 10** | 10.0 o superior | <https://dotnet.microsoft.com/download/dotnet/10.0> |
| **Node.js** | 22.12 o superior (o 20.19+) | <https://nodejs.org> |

Comprueba que quedaron instaladas (cierra y vuelve a abrir la terminal después de
instalar):

```bash
dotnet --version
```

```bash
node --version
```

El primero tiene que decir `10.x.x` y el segundo `v22.12` o superior (sirve también `v20.19`
o superior de la serie 20). Con un Node más viejo el sitio no arranca. Si dicen «no se
reconoce el comando», la instalación no agregó la herramienta al `PATH`: reinicia la
terminal, y si sigue igual, reinicia el equipo.

### Paso 2 · consigue el código

El repositorio es **privado**. Si tienes acceso:

```bash
git clone https://github.com/camilarodriguez10/modscore1.git
```

Si no, pide una copia de la carpeta del proyecto y descomprímela donde quieras. Entra en
ella:

```bash
cd modscore1
```

### Paso 3 · prepara el sitio

Una sola vez, desde la carpeta del proyecto, para bajar las dependencias del frontend. **Sin
este paso, la terminal 3 falla con `"vite" no se reconoce como un comando interno o
externo`.**

En `cmd`:

```bat
npm --prefix src\web ci --ignore-scripts
dir src\web\node_modules\.bin\vite.cmd
```

En PowerShell (con `npm.cmd`: `npm` a secas es un script que la política suele bloquear):

```powershell
npm.cmd --prefix src\web ci --ignore-scripts
Test-Path src\web\node_modules\.bin\vite.cmd
```

En macOS o Linux:

```bash
npm --prefix src/web ci --ignore-scripts
```

Tarda uno o dos minutos. Tiene que terminar con `found 0 vulnerabilities`, y la segunda línea
tiene que **encontrar** `vite.cmd` (en PowerShell, responder `True`). Si no lo encuentra, la
instalación falló: lee el error y busca en [Problemas frecuentes](#problemas-frecuentes).
`ci` instala exactamente las versiones del archivo de bloqueo, las mismas que se auditaron;
`--ignore-scripts` impide que un paquete ejecute código al instalarse.

### Paso 4 · levanta las tres piezas

Necesitas **tres terminales abiertas a la vez**, en la carpeta del proyecto. En cada una,
los comandos de tu sistema, y **en este orden**.

> **El entorno y el puerto no son opcionales**, y van **como argumentos, después de `--`**.
> Sin `--environment Development` la capa arranca como si fuera producción y se detiene al
> instante con un error sobre `AllowedHosts`: es a propósito, para que nadie publique la
> autenticación de desarrollo. Sin `--urls` escucha en el puerto 5000 y las capas no se
> encuentran. Escritos como argumentos, la línea es **la misma en cmd y en PowerShell**; no
> hace falta ningún `set`, que en PowerShell no funciona (ver
> [Problemas frecuentes](#problemas-frecuentes)).

**Terminal 1 — capa System.** En Windows, en cmd o en PowerShell:

```bat
dotnet run --project src\system\Modernization.SystemApi -- --environment Development --urls http://localhost:5101 --DataSource:Provider=File --DataSource:Directory=C:\modscore1\data\local
```

En macOS o Linux:

```bash
dotnet run --project src/system/Modernization.SystemApi -- --environment Development --urls http://localhost:5101 --DataSource:Provider=File --DataSource:Directory="$PWD/data/local"
```

Espera a ver `Now listening on: http://localhost:5101` y `Hosting environment: Development`.

Los dos últimos argumentos encienden la **carga de archivos**: lo que subas en la pantalla
Data queda en esa carpeta y sobrevive al reinicio. En Windows, si el proyecto no está en
`C:\modscore1`, cambia la ruta por `<carpeta del proyecto>\data\local`; tiene que ser
**absoluta** (ver [Cargar archivos](#cargar-archivos)).

**Terminal 2 — capa Channel.** En Windows, en cmd o en PowerShell:

```bat
dotnet run --project src\channel\Modernization.ChannelApi -- --environment Development --urls http://localhost:5100
```

En macOS o Linux:

```bash
dotnet run --project src/channel/Modernization.ChannelApi -- --environment Development --urls http://localhost:5100
```

Espera a ver `Now listening on: http://localhost:5100`.

**Terminal 3 — el sitio.** En cmd, macOS o Linux:

```bash
npm --prefix src/web run dev
```

En PowerShell:

```powershell
npm.cmd --prefix src\web run dev
```

Espera a ver `Local: http://localhost:5173/`.

La primera vez, los dos primeros comandos tardan porque compilan el proyecto entero. Las
siguientes veces arrancan en segundos.

### Paso 5 · entra y comprueba

Abre <http://localhost:5173> y pulsa **Sign in**. **La primera vez no hay datos**: cada
pantalla lo avisa y te lleva a **Data**. Sube ahí los ocho CSV de `data\dummy`, como se
explica en [Cargar archivos](#cargar-archivos). Luego, las mismas cuatro comprobaciones del
camino A:

1. Portfolio con unas 250 aplicaciones.
2. `http://localhost:5173/applications/14381/calculation` da **79.67**.
3. **Insights** muestra las seis preguntas.
4. **Dashboard** muestra cinco indicadores y seis paneles.

### Paso 6 · para detenerlo

En cada terminal, **Ctrl + C**. Ciérralas en el orden que quieras.

### Opcional · comprobar que todo el proyecto está sano

Con las terminales anteriores **cerradas** (si no, los archivos están en uso y la
compilación falla):

```bash
dotnet test ModernizationScore.slnx
```

```bash
npm --prefix src/web test
```

Deben terminar en verde: 737 pruebas .NET y 47 de la interfaz.

---

## Camino C · sin permiso para ejecutar `.exe` (Windows, `cmd`)

Para equipos de empresa donde Windows no deja ejecutar programas que no estén firmados: los
`.exe` del paquete portable no abren, y tampoco los que crea `dotnet run` por su cuenta.

La idea es que **el único programa que se ejecuta sea `dotnet.exe`**, que es de Microsoft y
está firmado. El código de la aplicación lo carga `dotnet` como biblioteca, y el sitio lo
sirve la propia capa Channel desde archivos HTML, JavaScript y CSS, que no se ejecutan. No
hace falta Node.

> Probado el 2026-09-17 en un clon limpio del repositorio, desde `cmd`: compila sin
> generar ningún `.exe` del proyecto, solo corren procesos `dotnet.exe`, y los escenarios de
> punta a punta pasan contra `http://localhost:5100`.

### Paso 1 · comprueba que `dotnet` sí corre

Abre `cmd` (tecla Windows, escribe `cmd`, Enter) y escribe:

```bat
dotnet --list-sdks
```

Tiene que aparecer al menos una línea que empiece con **10.** Si dice «no se reconoce» o no
hay ninguna 10, pide a soporte que instale el **.NET SDK 10** de Microsoft: es un instalador
firmado y suele estar en el catálogo de software de la empresa.

### Paso 2 · consigue el código

Igual que en el [paso 2 del camino B](#paso-2--consigue-el-código): `git clone`, o una copia
de la carpeta del proyecto. En esta guía se asume que quedó en `C:\modscore1`; si la pusiste
en otro lado, cambia esa ruta en todos los comandos.

### Paso 3 · consigue los archivos del sitio, sin compilar nada

El sitio ya compilado viene dentro del paquete portable. Solo hace falta esa carpeta:

1. Abre el `.zip` del paquete portable con doble clic (Windows lo muestra como carpeta, **sin
   ejecutar nada**).
2. Entra en `channel`.
3. Copia la carpeta **`wwwroot`** a `C:\modscore-sitio`.

Tiene que quedar el archivo `C:\modscore-sitio\wwwroot\index.html`. Compruébalo:

```bat
dir C:\modscore-sitio\wwwroot\index.html
```

### Paso 4 · compila una sola vez

En **una** ventana de `cmd`:

```bat
cd /d C:\modscore1
dotnet build src\channel\Modernization.ChannelApi -p:UseAppHost=false
```

Compila las dos capas —la Channel usa los contratos de la System— y tarda uno o dos minutos
la primera vez. **Tiene que terminar con `0 Errores`** (o `0 Error(s)` si Windows está en
inglés). Si termina con errores, la aplicación no va a arrancar: copia las líneas que dicen
`error` y ve a [Si la System no levanta](#si-la-system-no-levanta).

Compilar aparte, y no dentro de cada ventana, evita que las dos ventanas compilen a la vez
sobre los mismos archivos y se pisen.

### Paso 5 · levanta las dos capas, sin volver a compilar

Dos ventanas, **en este orden**. Copia cada bloque completo. La línea de `dotnet` es la misma
en cmd y en PowerShell; en PowerShell, la primera línea es `Set-Location C:\modscore1` en vez
de `cd /d C:\modscore1`.

**Ventana 1 — capa System:**

```bat
cd /d C:\modscore1
dotnet run --no-build --project src\system\Modernization.SystemApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5101 --DataSource:Provider=File --DataSource:Directory=C:\modscore1\data\local
```

Tiene que aparecer `Now listening on: http://localhost:5101`. **Compruébalo antes de seguir**:
abre <http://localhost:5101/health> en el navegador; tiene que decir `{"status":"healthy"}`.
Si no, no abras la ventana 2 todavía: ve a [Si la System no levanta](#si-la-system-no-levanta).

**Ventana 2 — capa Channel, que además sirve el sitio:**

```bat
cd /d C:\modscore1
dotnet run --no-build --project src\channel\Modernization.ChannelApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5100 --webroot C:\modscore-sitio\wwwroot
```

Tiene que aparecer `Now listening on: http://localhost:5100`.

> Si en la ventana 2 aparece `warn: ... Failed to determine the https port for redirect`,
> **no es un error**: en local todo va por HTTP y la aplicación solo avisa que no tiene a
> dónde redirigir. Desde el 2026-09-17 ya no se muestra al correr desde el código; el
> paquete portable anterior todavía lo imprime.

Qué hace cada parte que no estaba en el camino B:

| Parte | Para qué |
|---|---|
| `-p:UseAppHost=false` | Sin esto, `dotnet` fabrica un `Modernization.SystemApi.exe` propio y lo ejecuta, que es justo lo que la empresa bloquea. Con esto, quien ejecuta es `dotnet.exe`. Va **también** en `dotnet run`: sin él, `--no-build` busca ese `.exe`, que no existe |
| `--no-build` | Arranca lo que compilaste en el paso 4, sin volver a compilar |
| `--` | Lo que va antes es para `dotnet`; lo que va después, para la aplicación |
| `--webroot` | Le dice a la capa Channel dónde están los archivos del sitio, para servirlos ella misma en el puerto 5100. Por eso no hace falta Node ni una tercera ventana |
| `--DataSource:...` | Igual que en el camino B: carga de archivos, guardada en `C:\modscore1\data\local` |

### Si la System no levanta

Mira **las últimas líneas de la ventana 1**. Casi siempre dicen cuál de estos es:

| Si la ventana dice | Qué pasa | Qué hacer |
|---|---|---|
| `'AllowedHosts' debe listar los hosts del servicio (entorno 'Production')` | La capa no recibió `--environment Development`: faltó el argumento, o se usó un bloque viejo con `set` pegado en PowerShell | Copia de nuevo el bloque de la ventana 1 tal cual, con `-- --environment Development --urls ...` al final de la línea del `dotnet run` |
| `An error occurred trying to start process '...\Modernization.SystemApi.exe'` seguido de `no puede encontrar el archivo` o `acceso denegado` | Al `dotnet run` le falta `-p:UseAppHost=false`, y busca un `.exe` que no existe o que la empresa bloquea | Copia de nuevo el bloque de la ventana 1 tal cual: el `-p:UseAppHost=false` va en la línea del `dotnet run`, antes del `--` |
| `Failed to bind to address http://localhost:5101` o `address already in use` | Otro programa, o una System anterior que quedó abierta, ocupa el puerto | `netstat -ano \| findstr :5101` da el número de proceso al final de la línea; `taskkill /PID <número> /F` lo cierra |
| `The process cannot access the file ... because it is being used by another process` | Una System o Channel anterior sigue corriendo y tiene tomados los archivos | Cierra todas las ventanas de `cmd`, abre una nueva y repite desde el paso 4 |
| `MSB1009` o `Couldn't find a project to run` | La carpeta del `cd /d` no es la del proyecto | `dir C:\modscore1\src\system` tiene que listar `Modernization.SystemApi`; corrige la ruta |
| `NETSDK1045` o `does not support targeting .NET 10.0` | El SDK instalado es anterior al 10 | `dotnet --list-sdks`, y pedir a soporte el .NET SDK 10 |
| `NU1301` o `Unable to load the service index` | El proxy de la empresa no deja descargar los paquetes de nuget.org | Pedir a soporte acceso a `api.nuget.org`, o la configuración del proxy para NuGet |
| `error CA…` o `error IDE…` al compilar | Un SDK más nuevo que el del proyecto trae reglas de análisis nuevas, y el proyecto trata las advertencias como errores | Para correrlo en local: agrega `-p:TreatWarningsAsErrors=false` al `dotnet build` del paso 4 **y avísanos**, porque hay que ajustar el código |
| `No se puede ejecutar el programa` o `Acceso denegado` sobre `dotnet.exe` | La empresa bloquea también `dotnet` | Este camino no sirve en ese equipo: pedir a soporte que habilite el .NET SDK |
| La ventana vuelve al prompt sin decir nada | La aplicación terminó sin mensaje | Repite el bloque de la ventana 1 y copia **todo** lo que salga desde el `dotnet run` |

Si ninguna fila coincide, copia las últimas 20 líneas de la ventana 1 y mándalas: con eso
se identifica la causa.

### Paso 6 · entra y comprueba

Abre <http://localhost:5100> y pulsa **Sign in**. La primera vez, sube los ocho CSV de
`data\dummy` en **Data** ([Cargar archivos](#cargar-archivos)). Luego, las cuatro comprobaciones del
[paso 5 del camino A](#paso-5--comprueba-que-quedó-bien) cambiando nada más que el puerto:
Portfolio con ~250 aplicaciones, `/applications/14381/calculation` con **79.67**,
**Insights** con las seis preguntas y **Dashboard** con cinco indicadores y seis paneles.

### Paso 7 · para detenerlo

**Ctrl + C** en cada ventana. La próxima vez basta con el paso 5: no hace falta volver a
compilar mientras no cambie el código.

### Si prefieres compilar el sitio en vez de sacarlo del `.zip`

Con Node instalado:

```bat
npm --prefix src\web ci --ignore-scripts
```

```bat
npm --prefix src\web run build
```

y en la ventana 2 usa `--webroot C:\modscore1\src\web\dist`. **Ojo:** la
compilación ejecuta un `esbuild.exe` que viene dentro de las dependencias, y la misma
política que bloquea los `.exe` del paquete puede bloquear ese. Por eso el camino C usa por
defecto los archivos del `.zip`.

---

## Cargar archivos

La aplicación tiene dos modos de datos, y solo uno acepta cargas:

| Modo | Cómo se arranca | Data acepta cargas |
|---|---|---|
| **Archivos** | Caminos B y C tal como están arriba (`--DataSource:Provider=File --DataSource:Directory=...`); camino A con `.\start.cmd -Data File` | **Sí**. Cada carga queda como una versión y sobrevive al reinicio |
| **Datos generados** | Camino A con doble clic; caminos B y C sin los dos argumentos `--DataSource:...` | No: 250 aplicaciones inventadas en memoria, que se pierden al detener |

Si Data dice *The active adapter is 'Dummy', which serves generated data*, estás en el
segundo modo: detén la capa System y arráncala con la línea completa.

**Dónde queda lo cargado:** en los caminos B y C, en `C:\modscore1\data\local`, que git
ignora para que ningún dato real llegue al repositorio (las reglas publicadas quedan en
`.rules\`, dentro de la misma carpeta). En el camino A, en la carpeta `data\` del paquete.

> **La ruta tiene que ser absoluta.** `dotnet run --project` arranca con la carpeta del
> proyecto como raíz, así que una ruta relativa apunta a un directorio dentro de `src\`: el
> portafolio aparece vacío, sin ningún error que lo explique. Tampoco uses `data\history`,
> que está versionada: lo que subas ahí quedaría para el próximo commit.

**La primera carga.** Con la carpeta vacía, las pantallas dirán que no hay datos y te
llevarán a **Data**. Es lo esperado, no un error. Luego:

1. En **Upload files**, arrastra los ocho CSV (los de `samples\` en el paquete portable, o
   los de `data\dummy` en el repositorio), o los tuyos. **No hace falta renombrarlos**: cada
   archivo se reconoce por su encabezado. `eai_master.csv` es obligatorio.
2. Pulsa **Upload and recalculate**. La pantalla dice cuántas filas leyó, cuántas rechazó y
   por qué.
3. Con los CSV de ejemplo, Portfolio muestra 250 aplicaciones y la 14381 da **79.67**.

---

## Probar con un usuario limitado

Por defecto entras como administrador de todo. Para ver la aplicación como la vería alguien
que solo puede consultar un LOB, arranca la **capa Channel** así:

```bash
Authentication__Development__Roles__0=portfolio.read Authentication__Development__Scopes__0=lob:Individual dotnet run --project src/channel/Modernization.ChannelApi
```

Ese usuario no verá la pantalla **Rules** ni podrá publicar cargas, y todas las cifras
—incluidas las seis respuestas de **Insights**— se calculan solo sobre lo que su alcance
permite ver.

---

## Problemas frecuentes

| Lo que ves | Qué pasa | Qué hacer |
|---|---|---|
| `dotnet` o `npm` «no se reconoce» | No están instalados o la terminal es anterior a la instalación | Cierra y abre la terminal; si sigue, reinicia el equipo |
| En Data: `The active adapter is 'Dummy', which serves generated data. Uploading files requires the file or database adapter.` | La capa System arrancó sin carga de archivos | Caminos B y C: detén la ventana de la System y copia de nuevo su línea completa, con los dos `--DataSource:...`. Camino A: `.\start.cmd -Data File` |
| La consola se cierra sola al instante | Doble clic sobre `start.ps1` en vez de `start.cmd` | Usa `start.cmd`, o abre PowerShell y ejecuta `.\start.ps1` |
| `"start.cmd" no se reconoce como un comando interno o externo` (o lo mismo con un `.exe`), aunque el archivo está en esa carpeta | El equipo tiene la política `NoDefaultCurrentDirectoryInExePath`: `cmd` no busca programas en la carpeta actual | Escribe `.\start.cmd` (o `.\Modernization.SystemApi.exe`) |
| `No se puede cargar el archivo start.ps1 ... deshabilitada la ejecución de scripts` | Política de ejecución de PowerShell | `powershell -ExecutionPolicy Bypass -File .\start.ps1` |
| «Windows protegió su PC» | SmartScreen: los binarios no están firmados | Más información → Ejecutar de todas formas |
| «Este programa está bloqueado por una directiva de grupo», «el administrador del sistema bloqueó esta aplicación» o «acceso denegado» al abrir `start.cmd` o un `.exe` | La empresa no deja ejecutar programas sin firmar desde carpetas de usuario | Si el `.zip` llegó por internet o por correo, primero: clic derecho sobre el `.zip` → Propiedades → **Desbloquear**, y descomprímelo otra vez. Si sigue bloqueado, usa el **camino C** |
| `Unhandled exception ... 'AllowedHosts' debe listar los hosts del servicio (entorno 'Production')`, a veces con `DeploymentGuards.cs:line 32` y `Program.cs:line 214` | La capa arrancó como producción: no recibió el entorno de desarrollo. Lo más común es haber pegado un bloque de `cmd` con `set "..."` en **PowerShell** (la Terminal de Windows 11 abre PowerShell): ahí `set` crea una variable de PowerShell que el programa no ve, sin dar error | Usa los comandos con argumentos: al final de la línea, `--environment Development --urls http://localhost:5101` (después de `--` si es un `dotnet run`). Funcionan igual en cmd y en PowerShell. No hace falta tocar `AllowedHosts` |
| `warn: ... Failed to determine the https port for redirect` | Solo un aviso: en local no hay HTTPS al que redirigir | Nada: la capa está funcionando. Si el sitio falla, la causa es otra; revisa primero que la System responda en <http://localhost:5101/health> |
| `Now listening on: http://localhost:5000` | Faltó el argumento `--urls` | Detén con Ctrl + C y vuelve a arrancar con `--urls http://localhost:5101` (o `5100` en la Channel) al final de la línea |
| Camino C: `http://localhost:5100` responde 404 o una página vacía | `--webroot` no apunta a una carpeta con `index.html` | `dir C:\modscore-sitio\wwwroot\index.html` tiene que encontrar el archivo; corrige la ruta y reinicia la ventana 2 |
| `Failed to bind to address ... address already in use` | Otro programa ocupa 5100, 5101 o 5173 | Camino A: `.\start.ps1 -Port 5200`. Camino B: cierra lo que use el puerto (ver fila siguiente) |
| Necesitas saber **quién** ocupa un puerto | — | Windows: `netstat -ano \| findstr :5100` y luego `tasklist \| findstr <PID>`. macOS/Linux: `lsof -i :5100` |
| El sitio carga pero todo sale vacío o con error | La capa Channel está arriba, la System no | Mira la terminal 1: tiene que decir `Now listening on: http://localhost:5101` |
| El sitio no carga («no se puede acceder a este sitio») | La capa Channel no está arriba | Mira la terminal 2, y arráncala después de la System |
| `502` o `500` al entrar | Las dos capas no se están reconociendo | Deténlas todas y arráncalas en orden: System, Channel, sitio |
| Ves la página de login y `Sign in` no hace nada | La capa Channel no está en entorno `Development` | Caminos B y C: la línea de la Channel también necesita `--environment Development` |
| Al ejecutar un `.exe` a mano dice que no lo encuentra | Windows no busca en la carpeta actual | Escribe `.\Modernization.SystemApi.exe`, y hazlo **desde la carpeta de esa capa** |
| La ventana negra se cerró y los puertos siguen ocupados | Los procesos quedaron vivos | Camino A: `taskkill /IM Modernization.SystemApi.exe /F` y lo mismo con `Modernization.ChannelApi.exe`. Caminos B y C, donde el proceso se llama `dotnet.exe`: `netstat -ano \| findstr :5100` da el número de proceso, y `taskkill /PID <número> /F` lo cierra |
| Página en blanco tras actualizar el código | Faltan dependencias nuevas del sitio | Repite el [paso 3 del camino B](#paso-3--prepara-el-sitio) |
| `"vite" no se reconoce como un comando interno o externo` | Faltan las dependencias del sitio: el paso 3 del camino B no se hizo, falló o se hizo en otra carpeta | Desde la carpeta del proyecto, repite el paso 3 y comprueba que existe `src\web\node_modules\.bin\vite.cmd`. Si no vas a cambiar la interfaz, puedes saltarte Node: la Channel sirve el sitio ya compilado con `--webroot` (camino C, paso 5) |
| `npm.ps1 no se puede cargar ... la ejecución de scripts está deshabilitada` | En PowerShell, `npm` es un script que la política bloquea | Escribe `npm.cmd` en lugar de `npm` |
| El `npm ci` termina con `ECONNRESET`, `ETIMEDOUT`, `E403`, `self-signed certificate` o `unable to get local issuer certificate` | La red de la empresa no deja llegar al registro de npm | Pide a soporte el proxy o el registro interno de npm. Mientras tanto, usa la Channel sirviendo el sitio con `--webroot` (camino C), que no necesita Node |
| Vite avisa que la versión de Node no es compatible, o `crypto.hash is not a function` | Node anterior a 22.12 (o a 20.19) | Instala Node 22.12 o superior y repite el paso 3 |
| `dotnet test` falla con «el archivo está siendo usado por otro proceso» | La aplicación está corriendo | Detén las tres piezas antes de compilar o probar |
| Todo iba bien y dejó de arrancar tras mover la carpeta | Ruta dentro de OneDrive o demasiado larga | Muévela a algo como `C:\modscore` |

---

## Cómo dejar el equipo como estaba

1. Detén la aplicación (Enter en la consola, o Ctrl + C en las tres terminales).
2. Borra la carpeta donde descomprimiste el paquete (camino A) o donde clonaste el
   repositorio (camino B).
3. Lo que hayas cargado se va con esa carpeta: en el camino A está en `data\`, y en los
   caminos B y C en `C:\modscore1\data\local` (si usaste otra ruta, bórrala aparte).

No queda nada instalado ni registrado en el sistema: el paquete portable no toca el
registro de Windows ni instala servicios.

---

## Checklist

- [ ] Las tres piezas arriba (o las dos del paquete portable).
- [ ] `Sign in` entra sin pedir contraseña.
- [ ] En los caminos B y C, Data acepta la primera carga con los CSV de `data\dummy`.
- [ ] Portfolio muestra ~250 aplicaciones.
- [ ] `/applications/14381/calculation` da **79.67**.
- [ ] **Insights** responde las seis preguntas.
- [ ] **Dashboard** muestra cinco indicadores y seis paneles, y un clic abre el detalle.
- [ ] **Code** avisa en ámbar que los repositorios son de demostración y muestra las fechas de
      fin de vida de endoflife.date (caminos B y C; en el paquete portable, desde el generado
      el 2026-09-26).
- [ ] Al detener, ninguna ventana queda abierta y los puertos quedan libres.

---

## Para seguir

- [README.md](../README.md) — qué hace la plataforma y cómo se verifica.
- [tools/portable/README.md](../tools/portable/README.md) — detalle del paquete portable y
  cómo generar uno nuevo.
- [HANDOFF.md](../HANDOFF.md) — el contexto completo del proyecto.
- [docs/runbooks](runbooks) — procedimientos de operación (rotación de secretos, carga
  defectuosa, recuperación, reglas defectuosas).
