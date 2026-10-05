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
| **Sitio (SPA)** | Lo que ves en pantalla | `5173` en el camino A; en los caminos B y C lo sirve la capa Channel |

Hay **tres caminos**, todos desde el código y con comandos para `cmd` y PowerShell. Elige uno:

| | Camino A · desde el código | Camino B · sin permiso para ejecutar `.exe` | Camino C · sin Vite ni Node |
|---|---|---|---|
| Para quién | Cualquiera que va a **usar o modificar** la aplicación | Un equipo de empresa que **bloquea programas no firmados** | Quien solo va a **usar** la aplicación y no puede instalar Node, o donde Vite no arranca |
| Sistema | Windows, macOS o Linux | Windows, desde `cmd` o PowerShell | Windows, desde `cmd` o PowerShell |
| Hay que instalar | .NET SDK 10 y Node.js | .NET SDK 10 (Node solo para compilar el sitio una vez, en este equipo o en otro) | **Solo .NET SDK 10**. El sitio ya viene compilado con el código, en `sitio-compilado` |
| Qué se ejecuta | `dotnet` y `node` (Vite) | **Solo `dotnet.exe`**, firmado por Microsoft | **Solo `dotnet.exe`**. Ni Node ni Vite |
| Ventanas abiertas | Tres | Dos | Dos |
| Cuánto toma | 20 minutos la primera vez | 15 minutos la primera vez | 10 minutos la primera vez |
| Se abre en | `http://localhost:5173` | `http://localhost:5100` | `http://localhost:5100` |

Si dudas, empieza por el **camino A**. Si al arrancar Windows dice que un programa está
**bloqueado por el administrador** o que **no tienes permiso**, ve al **camino B**. Si no
puedes instalar Node, el `npm ci` no llega al registro de npm o la ventana de Vite nunca dice
`ready`, ve al **camino C**.

---

## Camino A · desde el código (Windows, macOS o Linux)

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

O, sin git, en la página del repositorio: **Code → Download ZIP**, y descomprímelo donde
quieras (la carpeta se llama `modscore1-main`). Sin acceso, pide una copia de la carpeta del
proyecto. Entra en ella:

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

Abre <http://localhost:5173> y pulsa **Sign in**. No pide usuario ni contraseña: el esquema
de desarrollo te entra con todos los permisos.

**La primera vez no hay datos**: cada pantalla lo avisa y te lleva a **Data**. Hay dos
formas de cargar, explicadas en [Cargar archivos](#cargar-archivos): subir los ocho CSV de
`data\dummy`, o copiar antes de arrancar los cuatro trimestres de `data\timeline` para ver
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
   compara el score, el Currency Score y el riesgo carga por carga.

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

Deben terminar en verde: 937 pruebas .NET y 74 de la interfaz.

---

## Camino B · sin permiso para ejecutar `.exe` (Windows, `cmd`)

Para equipos de empresa donde Windows no deja ejecutar programas que no estén firmados: los
`.exe` que crea `dotnet run` por su cuenta no abren.

La idea es que **el único programa que se ejecuta sea `dotnet.exe`**, que es de Microsoft y
está firmado. El código de la aplicación lo carga `dotnet` como biblioteca, y el sitio lo
sirve la propia capa Channel desde archivos HTML, JavaScript y CSS ya compilados, que no se
ejecutan. Node hace falta una sola vez, para compilar el sitio, y puede hacerse en otro
equipo.

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

Igual que en el [paso 2 del camino A](#paso-2--consigue-el-código): `git clone`, o una copia
de la carpeta del proyecto. En esta guía se asume que quedó en `C:\modscore1`; si la pusiste
en otro lado, cambia esa ruta en todos los comandos.

### Paso 3 · compila el sitio una sola vez

El sitio compilado es una carpeta de archivos estáticos, `src\web\dist`. Se genera con Node,
**en este equipo o en cualquier otro** que tenga Node 22.12 o superior y acceso al registro
de npm.

En `cmd`:

```bat
cd /d C:\modscore1
npm --prefix src\web ci --ignore-scripts
npm --prefix src\web run build
dir src\web\dist\index.html
```

En PowerShell:

```powershell
Set-Location C:\modscore1
npm.cmd --prefix src\web ci --ignore-scripts
npm.cmd --prefix src\web run build
Test-Path src\web\dist\index.html
```

El último comando tiene que encontrar `index.html` (en PowerShell, responder `True`). Si lo
compilaste en otro equipo, copia la carpeta `dist` completa a `C:\modscore1\src\web\dist`
en este. La compilación ejecuta un `esbuild.exe` que viene con las dependencias: si la misma
política que bloquea los `.exe` lo bloquea, compílalo en otro equipo o **descárgalo ya
compilado**, sin Node, como en el [paso 2 del camino C](#paso-2--descarga-el-sitio-ya-compilado).

### Paso 4 · compila las dos capas una sola vez

En **una** ventana (en PowerShell, la primera línea es `Set-Location C:\modscore1`):

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
dotnet run --no-build --project src\channel\Modernization.ChannelApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5100 --webroot C:\modscore1\src\web\dist
```

Tiene que aparecer `Now listening on: http://localhost:5100`.

> Si en la ventana 2 aparece `warn: ... Failed to determine the https port for redirect`,
> **no es un error**: en local todo va por HTTP y la aplicación solo avisa que no tiene a
> dónde redirigir.

Qué hace cada parte que no estaba en el camino A:

| Parte | Para qué |
|---|---|
| `-p:UseAppHost=false` | Sin esto, `dotnet` fabrica un `Modernization.SystemApi.exe` propio y lo ejecuta, que es justo lo que la empresa bloquea. Con esto, quien ejecuta es `dotnet.exe`. Va **también** en `dotnet run`: sin él, `--no-build` busca ese `.exe`, que no existe |
| `--no-build` | Arranca lo que compilaste en el paso 4, sin volver a compilar |
| `--` | Lo que va antes es para `dotnet`; lo que va después, para la aplicación |
| `--webroot` | Le dice a la capa Channel dónde están los archivos del sitio, para servirlos ella misma en el puerto 5100. Por eso no hace falta Node ni una tercera ventana |
| `--DataSource:...` | Igual que en el camino A: carga de archivos, guardada en `C:\modscore1\data\local` |

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

Abre <http://localhost:5100> y pulsa **Sign in**. La primera vez, carga los datos como se
explica en [Cargar archivos](#cargar-archivos). Luego, las cinco comprobaciones del
[paso 5 del camino A](#paso-5--entra-y-comprueba) cambiando nada más que el puerto: 5100 en
lugar de 5173.

### Paso 7 · para detenerlo

**Ctrl + C** en cada ventana. La próxima vez basta con el paso 5: no hace falta volver a
compilar mientras no cambie el código.

---

## Camino C · sin Vite ni Node (Windows, `cmd` o PowerShell)

Para **usar** la aplicación sin instalar Node, sin ejecutar Vite y sin bajar nada del registro
de npm. El sitio son archivos HTML, JavaScript y CSS que no se ejecutan, y **viajan con el
código** en la carpeta `sitio-compilado`: el ZIP de GitHub ya los trae. En el equipo solo corre
`dotnet.exe`, igual que en el camino B, así que también sirve donde la empresa bloquea los
`.exe` propios o los paquetes nuevos de npm.

El CI comprueba en cada cambio que `sitio-compilado` es exactamente el build del código de
`src\web` de ese mismo commit, así que sitio y capas siempre son de la misma versión.

Lo que no se puede: ver al vuelo un cambio en `src\web`. Para tocar la interfaz, camino A.

### Paso 1 · comprueba `dotnet` y consigue el código

Son los pasos [1](#paso-1--comprueba-que-dotnet-sí-corre) y
[2](#paso-2--consigue-el-código-1) del camino B: `dotnet --list-sdks` tiene que listar una
versión `10.`, y el código queda en `C:\modscore1`. Sirve igual el ZIP de GitHub
(**Code → Download ZIP**) que un `git clone`.

### Paso 2 · comprueba que el sitio vino con el código

En `cmd`:

```bat
dir C:\modscore1\sitio-compilado\index.html
```

En PowerShell:

```powershell
Test-Path C:\modscore1\sitio-compilado\index.html
```

Tiene que encontrar `index.html` (en PowerShell, responder `True`). Si no existe la carpeta,
el código es anterior al 2026-10-05: descarga de nuevo el ZIP de `main`.

### Paso 3 · compila y levanta las dos capas

Igual que en el camino B. En PowerShell, la primera línea de cada bloque es
`Set-Location C:\modscore1` en vez de `cd /d C:\modscore1`.

**Una sola vez, compilar** (tiene que terminar con `0 Errores` o `0 Error(s)`):

```bat
cd /d C:\modscore1
dotnet build src\channel\Modernization.ChannelApi -p:UseAppHost=false
```

**Ventana 1 — capa System.** Antes de seguir, <http://localhost:5101/health> tiene que decir
`{"status":"healthy"}`:

```bat
cd /d C:\modscore1
dotnet run --no-build --project src\system\Modernization.SystemApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5101 --DataSource:Provider=File --DataSource:Directory=C:\modscore1\data\local
```

**Ventana 2 — capa Channel, que sirve el sitio compilado:**

```bat
cd /d C:\modscore1
dotnet run --no-build --project src\channel\Modernization.ChannelApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5100 --webroot C:\modscore1\sitio-compilado
```

Qué hace cada parte está en el [paso 5 del camino B](#paso-5--levanta-las-dos-capas-sin-volver-a-compilar);
si una capa no arranca, [Si la System no levanta](#si-la-system-no-levanta).

### Paso 4 · entra y comprueba

Abre <http://localhost:5100> y pulsa **Sign in**. La primera vez, carga los datos como se
explica en [Cargar archivos](#cargar-archivos), y haz las cinco comprobaciones del
[paso 5 del camino A](#paso-5--entra-y-comprueba) con el puerto 5100 en lugar de 5173.

Para detenerlo, **Ctrl + C** en cada ventana; la próxima vez basta con las dos ventanas del
paso 3. Si actualizas el código (`git pull` o un ZIP nuevo), repite la compilación del paso 3:
el sitio compilado llega con el código, de la misma versión.

---

## Cargar archivos

La aplicación tiene dos modos de datos, y solo uno acepta cargas:

| Modo | Cómo se arranca | Data acepta cargas |
|---|---|---|
| **Archivos** | Los tres caminos tal como están arriba (`--DataSource:Provider=File --DataSource:Directory=...`) | **Sí**. Cada carga queda como una versión y sobrevive al reinicio |
| **Datos generados** | Los tres caminos sin los dos argumentos `--DataSource:...` | No: 250 aplicaciones inventadas en memoria, con sus cuatro trimestres, que se pierden al detener |

Si Data dice *The active adapter is 'Dummy', which serves generated data*, estás en el
segundo modo: detén la capa System y arráncala con la línea completa.

**Dónde queda lo cargado:** en `C:\modscore1\data\local`, que git ignora para que ningún
dato real llegue al repositorio (las reglas publicadas quedan en `.rules\`, dentro de la
misma carpeta).

> **La ruta tiene que ser absoluta.** `dotnet run --project` arranca con la carpeta del
> proyecto como raíz, así que una ruta relativa apunta a un directorio dentro de `src\`: el
> portafolio aparece vacío, sin ningún error que lo explique. Tampoco uses `data\history`,
> que está versionada: lo que subas ahí quedaría para el próximo commit.

**La primera carga.** Con la carpeta vacía, las pantallas dirán que no hay datos y te
llevarán a **Data**. Es lo esperado, no un error. Luego:

1. En **Upload files**, arrastra los ocho CSV de `data\dummy`, o los tuyos. **No hace falta
   renombrarlos**: cada archivo se reconoce por su encabezado. `eai_master.csv` es
   obligatorio.
2. Pulsa **Upload and recalculate**. La pantalla dice cuántas filas leyó, cuántas rechazó y
   por qué.
3. Con los CSV de ejemplo, Portfolio muestra 250 aplicaciones y la 14381 da **79.67**.

**Con tus propios exports: revisa el mapping antes de cargar.** Si tus archivos no traen
exactamente los nombres de columna esperados, decide primero qué columna carga cada campo:

1. En **Data → Uploads**, elige los archivos y pulsa **Review column mapping**. El navegador
   lee **solo la primera línea** de cada archivo (los nombres de las columnas): ninguna fila
   de datos sale del equipo en este paso.
2. Para cada archivo verás su fuente y, por cada campo de la base, la columna que lo
   cargaría y cómo se decidió: *Same name*, *Known alias*, *As in the last upload* o *Not in
   this file*. Si una columna tuya no aparece donde debe, elígela en el selector del campo;
   las columnas que no cargan nada se listan debajo de la tabla.
3. Pulsa **Upload with this mapping**. El mapping queda guardado con la carga y la próxima
   vez se propone solo.

La pestaña **Data → Mapping** muestra, sin elegir archivos, de qué columna sale cada campo en
la carga vigente, con qué nombres se reconoce solo y qué métricas lo usan.

**Exports de Power BI o de Excel en español.** Se cargan tal como salen: los campos
«Suma de …» se reconocen, `N/A`, `-` o `(En blanco)` cuentan como celda vacía, los números se
leen con coma o punto decimal y `VERDADERO`/`FALSO` como sí/no. Si el export trae «MFA» y
«Does this Application Utilize MFA?», carga el segundo; Year, Quarter, Month y Day no se usan.
Si un EAI se repite (una fila por fecha), queda la última fila.

**Si la carga rechaza filas o deja valores sin dato.** Debajo del detalle por archivo,
**Why rows were rejected** y **Values loaded as empty** dicen qué pasó, agrupado por problema:
la columna, qué esperaba, el valor de la primera fila que lo tuvo y cuántas filas. No hace
falta compartir el archivo para entenderlo. Un valor ilegible en un campo opcional ya no
rechaza la fila: el campo queda sin dato y la aplicación entra. Los casos más comunes:

| La pantalla dice | Qué hacer en el archivo |
|---|---|
| `... expected an integer. For example, '712.5'` | Esa columna lleva números enteros: redondéalos en el export |
| `... expected an integer. For example, '100-500'` | La columna trae rangos o texto, no números: ese campo queda sin dato hasta que el export traiga el número |
| Una columna de números con texto en **todas** las filas | El campo se cargó desde otra columna —revisa el mapping— o las columnas están corridas, por una coma dentro de un valor sin comillas |
| `No value in the key column 'EAI_Code'` | Filas sin EAI: totales, subtotales o líneas separadoras exportadas con los datos |
| `'EAI_Code' 14381 appears more than once` | El export trae una fila por fecha: exporta solo la última, u ordénalo de la más antigua a la más reciente |

Si el EAI Master queda sin ninguna fila válida, la carga se descarta completa y el portafolio
vigente no cambia.

**Sin `APP_ID` en el EAI Master**, la deuda técnica se une por el EAI_Code, porque en la
organización el APP_ID es el mismo código que el EAI. Si el maestro trae `APP_ID`, se usa esa
columna.

**Para comparar en el tiempo: los cuatro trimestres de ejemplo.** `data\timeline` trae el
mismo portafolio en cuatro cargas —Q4 2025, Q1, Q2 y Q3 2026—, con aplicaciones que se
modernizan y otras que envejecen. Cópialas a `data\local` **antes de arrancar la capa
System** (si ya estaba corriendo, detenla con Ctrl + C y vuelve a arrancarla).

En `cmd`:

```bat
cd /d C:\modscore1
xcopy /E /I /Y data\timeline data\local
```

En PowerShell:

```powershell
Set-Location C:\modscore1
New-Item -ItemType Directory -Force data\local | Out-Null
Copy-Item -Recurse -Force data\timeline\* data\local
```

Después, en **Data → Upload history** aparecen las cuatro cargas, y la pantalla **Code**
compara el Modernization Score, el Currency Score y el Risk Score de una a otra; el detalle
de cada aplicación muestra su propia evolución.

---

## Probar con un usuario limitado

Por defecto entras como administrador de todo. Para ver la aplicación como la vería alguien
que solo puede consultar un LOB, agrega dos argumentos al final de la línea de la **capa
Channel** (la misma línea en cmd y en PowerShell):

```bat
dotnet run --no-build --project src\channel\Modernization.ChannelApi -- --environment Development --urls http://localhost:5100 --Authentication:Development:Roles:0=portfolio.read --Authentication:Development:Scopes:0=lob:Individual
```

Ese usuario no verá la pantalla **Rules** ni podrá publicar cargas, y todas las cifras
—incluidas las seis respuestas de **Insights**— se calculan solo sobre lo que su alcance
permite ver.

---

## Problemas frecuentes

| Lo que ves | Qué pasa | Qué hacer |
|---|---|---|
| `dotnet` o `npm` «no se reconoce» | No están instalados o la terminal es anterior a la instalación | Cierra y abre la terminal; si sigue, reinicia el equipo |
| En Data: `The active adapter is 'Dummy', which serves generated data. Uploading files requires the file or database adapter.` | La capa System arrancó sin carga de archivos | Detén la ventana de la System y copia de nuevo su línea completa, con los dos `--DataSource:...` |
| «Este programa está bloqueado por una directiva de grupo», «el administrador del sistema bloqueó esta aplicación» o «acceso denegado» al arrancar una capa | La empresa no deja ejecutar programas sin firmar desde carpetas de usuario | Usa el **camino B**: solo corre `dotnet.exe` |
| `Unhandled exception ... 'AllowedHosts' debe listar los hosts del servicio (entorno 'Production')`, a veces con `DeploymentGuards.cs:line 32` y `Program.cs:line 214` | La capa arrancó como producción: no recibió el entorno de desarrollo. Lo más común es haber pegado un bloque de `cmd` con `set "..."` en **PowerShell** (la Terminal de Windows 11 abre PowerShell): ahí `set` crea una variable de PowerShell que el programa no ve, sin dar error | Usa los comandos con argumentos: al final de la línea, `--environment Development --urls http://localhost:5101` (después de `--` si es un `dotnet run`). Funcionan igual en cmd y en PowerShell. No hace falta tocar `AllowedHosts` |
| `warn: ... Failed to determine the https port for redirect` | Solo un aviso: en local no hay HTTPS al que redirigir | Nada: la capa está funcionando. Si el sitio falla, la causa es otra; revisa primero que la System responda en <http://localhost:5101/health> |
| `Now listening on: http://localhost:5000` | Faltó el argumento `--urls` | Detén con Ctrl + C y vuelve a arrancar con `--urls http://localhost:5101` (o `5100` en la Channel) al final de la línea |
| Caminos B y C: `http://localhost:5100` responde 404 o una página vacía | `--webroot` no apunta a una carpeta con `index.html` | Camino B: `dir C:\modscore1\src\web\dist\index.html` tiene que encontrar el archivo; si no, repite el paso 3 y reinicia la ventana 2. Camino C: `dir C:\modscore1\sitio-compilado\index.html`, y que `--webroot` diga esa misma ruta |
| `Failed to bind to address ... address already in use` | Otro programa ocupa 5100, 5101 o 5173 | Cierra lo que use el puerto (ver fila siguiente), o cambia el número en `--urls` |
| Necesitas saber **quién** ocupa un puerto | — | Windows: `netstat -ano \| findstr :5100` y luego `tasklist \| findstr <PID>`. macOS/Linux: `lsof -i :5100` |
| El sitio carga pero todo sale vacío o con error | La capa Channel está arriba, la System no | Mira la terminal 1: tiene que decir `Now listening on: http://localhost:5101` |
| El sitio no carga («no se puede acceder a este sitio») | La capa Channel no está arriba | Mira la terminal 2, y arráncala después de la System |
| `502` o `500` al entrar | Las dos capas no se están reconociendo | Deténlas todas y arráncalas en orden: System, Channel, sitio |
| Ves la página de login y `Sign in` no hace nada | La capa Channel no está en entorno `Development` | En todos los caminos, la línea de la Channel también necesita `--environment Development` |
| Se cerró una ventana y el puerto sigue ocupado | El proceso quedó vivo | `netstat -ano \| findstr :5100` da el número de proceso al final de la línea, y `taskkill /PID <número> /F` lo cierra (en PowerShell, `Get-NetTCPConnection -LocalPort 5100 -State Listen` y `Stop-Process -Id <número>`) |
| Página en blanco tras actualizar el código | Faltan dependencias nuevas del sitio | Repite el [paso 3 del camino A](#paso-3--prepara-el-sitio) |
| `"vite" no se reconoce como un comando interno o externo` | Faltan las dependencias del sitio: el paso 3 del camino A no se hizo, falló o se hizo en otra carpeta | Desde la carpeta del proyecto, repite el paso 3 y comprueba que existe `src\web\node_modules\.bin\vite.cmd`. Si no vas a cambiar la interfaz, sigue el **camino C**: sin Node ni Vite |
| `npm.ps1 no se puede cargar ... la ejecución de scripts está deshabilitada` | En PowerShell, `npm` es un script que la política bloquea | Escribe `npm.cmd` en lugar de `npm` |
| El `npm ci` termina con `UNABLE_TO_GET_ISSUER_CERT_LOCALLY`, `unable to get local issuer certificate`, `SELF_SIGNED_CERT_IN_CHAIN` o `self-signed certificate in certificate chain` | La red de la empresa inspecciona el tráfico HTTPS y lo vuelve a firmar con un certificado propio. Windows y el navegador lo reconocen; Node no, porque trae su propia lista. Suele aparecer después de una actualización de dependencias: lo que ya estaba en la caché de npm no sale a internet, y lo nuevo sí | En la misma ventana, haz que Node use también los certificados de Windows y repite el paso: en cmd `set NODE_OPTIONS=--use-system-ca`, en PowerShell `$env:NODE_OPTIONS="--use-system-ca"`. Hace falta Node 22.15 o superior (`node --version`) y dura lo que dure la ventana. Si sigue fallando, pide a soporte el certificado raíz de la empresa en Base-64 (`.pem` o `.cer`) y usa `set NODE_EXTRA_CA_CERTS=C:\ruta\raiz.pem`. **Nunca** `strict-ssl=false` ni `NODE_TLS_REJECT_UNAUTHORIZED=0`: apagan la verificación de todos los certificados. O sigue el **camino C**, que no usa npm |
| El `npm ci` termina con `E403` · `403 Forbidden - GET https://registry.npmjs.org/<paquete>/-/<paquete>-<versión>.tgz` | El registro público sí entrega esa versión: la bloquea la política de seguridad de la red de la empresa. Muchas bloquean las versiones publicadas hace poco, y las correcciones de seguridad son justo las más recientes | Pide a soporte que habilite **esa versión exacta** (la que dice el error) e indica que corrige vulnerabilidades. **No** bajes a una versión anterior: el lock fija la que no tiene vulnerabilidades conocidas. Mientras tanto, sigue el **camino C**, que no usa npm |
| El `npm ci` termina con `ECONNRESET` o `ETIMEDOUT` | La red de la empresa no deja llegar al registro de npm | Pide a soporte el proxy o el registro interno de npm. Mientras tanto, sigue el **camino C**, que no usa npm |
| Vite avisa que la versión de Node no es compatible, o `crypto.hash is not a function` | Node anterior a 22.12 (o a 20.19) | Instala Node 22.12 o superior y repite el paso 3; o sigue el **camino C**, sin Node |
| La ventana de Vite nunca dice `ready`, o `src\web\node_modules\.bin\esbuild.cmd --version` se queda colgado o dice «acceso denegado» | La política de la empresa bloquea el `esbuild.exe` que Vite ejecuta por dentro, o el antivirus revisa uno a uno los archivos de `node_modules` | Sigue el **camino C**: el sitio llega compilado y no se ejecuta nada de Node. Si quieres el camino A, pide a soporte una exclusión para la carpeta del proyecto |
| Camino C: no existe la carpeta `sitio-compilado` | El código es anterior al 2026-10-05, cuando el sitio compilado empezó a viajar con él | Descarga de nuevo el ZIP de `main` (o `git pull`) |
| `dotnet test` falla con «el archivo está siendo usado por otro proceso» | La aplicación está corriendo | Detén todas las ventanas antes de compilar o probar |
| Todo iba bien y dejó de arrancar tras mover la carpeta | Ruta dentro de OneDrive o demasiado larga | Muévela a algo como `C:\modscore` |
| En Data, el EAI Master queda con `0` filas y todas rechazadas, y la carga se descarta | Desde el 2026-10-02 solo pasa si a todas les falta el EAI; un valor ilegible en otro campo ya no rechaza la fila | Lee **Why rows were rejected**; si el EAI viene en otra columna, elígela con **Review column mapping** |

---

## Cómo dejar el equipo como estaba

1. Detén la aplicación: Ctrl + C en cada ventana.
2. Borra la carpeta donde clonaste el repositorio.
3. Lo que hayas cargado se va con esa carpeta, en `C:\modscore1\data\local` (si usaste otra
   ruta, bórrala aparte).

La aplicación no instala servicios ni toca el registro de Windows. Lo único instalado son el
.NET SDK y, en los caminos A y B, Node, que se desinstalan como cualquier programa.

---

## Checklist

- [ ] Las tres piezas arriba (o las dos ventanas de los caminos B y C).
- [ ] `Sign in` entra sin pedir contraseña.
- [ ] Data acepta la primera carga con los CSV de `data\dummy`, o muestra los cuatro
      trimestres copiados de `data\timeline`.
- [ ] Portfolio muestra ~250 aplicaciones.
- [ ] `/applications/14381/calculation` da **79.67**.
- [ ] **Insights** responde las seis preguntas.
- [ ] **Dashboard** muestra cinco indicadores y seis paneles, y un clic abre el detalle.
- [ ] **Code** avisa en ámbar que los repositorios son de demostración y muestra la
      proyección a uno, dos y tres años, el plan de mejora y, con los cuatro trimestres, la
      evolución por carga.
- [ ] Al detener, ninguna ventana queda abierta y los puertos quedan libres.

---

## Para seguir

- [README.md](../README.md) — qué hace la plataforma y cómo se verifica.
- [HANDOFF.md](../HANDOFF.md) — el contexto completo del proyecto.
- [docs/runbooks](runbooks) — procedimientos de operación (rotación de secretos, carga
  defectuosa, recuperación, reglas defectuosas).
