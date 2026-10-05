# Modernization Score Platform

Recreación del cálculo de **Modernization Score v1.3** sobre fuentes dummy, con
backend .NET 10 en dos capas API-led y SPA React 19.

El plan completo está en [PLAN.md](PLAN.md). Las decisiones de arquitectura, en
[docs/adr](docs/adr).

## Levantar el stack

> ¿Primera vez, o hay que levantarlo en otro equipo? La guía paso a paso, sin dar nada por
> sabido y con los problemas frecuentes, está en
> [docs/guia-localhost.md](docs/guia-localhost.md).

### Requisitos: qué instalar antes

| Cómo se levanta | Qué hay que tener instalado |
|---|---|
| **Los comandos de abajo**, en tres ventanas | **.NET SDK 10** (<https://dotnet.microsoft.com/download/dotnet/10.0>) y **Node.js 22.12 o superior**, o 20.19+ (<https://nodejs.org>) |
| **Con el sitio compilado**, en dos ventanas (variante de abajo) | **.NET SDK 10**. Node solo hace falta para compilar el sitio una vez, y eso puede hacerse en otro equipo y copiar la carpeta `src\web\dist` |
| **Sin Vite ni Node**, en dos ventanas ([abajo](#sin-vite-ni-node-el-sitio-ya-compilado)) | **Solo .NET SDK 10**. El sitio compilado viene con el código, en `sitio-compilado` (también en el ZIP de GitHub) |

Hace falta el **SDK** de .NET, no solo el runtime: el runtime corre programas ya compilados,
pero no compila. Para comprobarlo, en una terminal abierta después de instalar:

```bat
dotnet --list-sdks
```

```bat
node --version
```

El primero tiene que listar una versión `10.x`, y el segundo decir `v22.12` o más. Si alguno
responde «no se reconoce como un comando interno o externo», falta instalarlo, o la terminal
se abrió antes de la instalación: ciérrala y abre otra.

### Qué se levanta

Tres procesos: la capa **System** en el puerto 5101, la capa **Channel** en el 5100 y la
**SPA** en el 5173. Se compila una vez y después se abren **tres ventanas, en este orden**,
cada una abierta mientras se usa la aplicación.

- El entorno y el puerto van **como argumentos de la aplicación**, después de `--`:
  `-- --environment Development --urls http://localhost:5101`. Así la línea es la misma en
  cmd, PowerShell y Git Bash. Sin `--environment Development` la capa arranca como
  producción y se detiene a propósito (`'AllowedHosts' debe listar los hosts del servicio`);
  sin `--urls`, escucha en el 5000.
- **La System primero, y la Channel cuando la System ya responda.** Compilar antes, aparte, evita
  que las dos ventanas compilen a la vez: la Channel referencia el proyecto de la System, y
  si compilan juntas se pisan el mismo `.dll` (`CS2012`, archivo en uso).
- **La primera vez, instalar las dependencias del sitio** (el paso «una sola vez» de cada
  consola, abajo). Sin ellas, la ventana 3 falla con `"vite" no se reconoce como un comando
  interno o externo`. Hace falta **Node.js 22.12 o superior** (o 20.19+): `node --version`.
- **¿Solo vas a usar la aplicación, no a cambiar la interfaz?** Se puede prescindir de la
  ventana 3: la Channel sirve el sitio compilado. Se compila una vez con Node (ver
  [Variantes](#variantes), «Con el sitio compilado») o se usa el que ya viene compilado con
  el código, sin Node ni Vite (ver [Sin Vite ni Node](#sin-vite-ni-node-el-sitio-ya-compilado)).
- **La System arranca con carga de archivos** (`--DataSource:Provider=File`): la pantalla
  **Data** acepta CSV, y lo que subas y las reglas que publiques quedan en
  `C:\modscore1\data\local`. Git ignora esa carpeta porque puede tener datos reales. La
  primera vez está vacía: ver [La primera carga](#la-primera-carga). Sin esos dos argumentos,
  la System inventa 250 aplicaciones en memoria y **Data no acepta cargas**.

Los ejemplos suponen el repositorio en `C:\modscore1`; cambia esa ruta si está en otro lado.
Las líneas de `dotnet` son idénticas en cmd y en PowerShell; solo cambia cómo se entra a la
carpeta (`cd /d` en cmd, `Set-Location` en PowerShell).

### En cmd

**Una sola vez — compilar e instalar las dependencias del sitio:**

```bat
cd /d C:\modscore1
dotnet build src\channel\Modernization.ChannelApi
npm --prefix src\web ci --ignore-scripts
dir src\web\node_modules\.bin\vite.cmd
```

El `dotnet build` tiene que terminar con `0 Errores` (o `0 Error(s)`), el `npm ci` con
`found 0 vulnerabilities`, y el `dir` tiene que **encontrar** `vite.cmd`. Si dice «No se
encuentra el archivo», la instalación no terminó: lee el error del `npm ci` (ver
[Si algo falla](#si-algo-falla)). Compilar hay que repetirlo cuando cambia el código;
instalar, cuando cambia `src\web\package-lock.json`.

**Ventana 1 — capa System:**

```bat
cd /d C:\modscore1
dotnet run --no-build --project src\system\Modernization.SystemApi -- --environment Development --urls http://localhost:5101 --DataSource:Provider=File --DataSource:Directory=C:\modscore1\data\local
```

Tiene que aparecer `Hosting environment: Development`. Antes de seguir, en otra ventana:
`curl http://localhost:5101/health` tiene que responder `{"status":"healthy"}`.

**Ventana 2 — capa Channel:**

```bat
cd /d C:\modscore1
dotnet run --no-build --project src\channel\Modernization.ChannelApi -- --environment Development --urls http://localhost:5100
```

**Ventana 3 — la SPA:**

```bat
cd /d C:\modscore1
npm --prefix src\web run dev
```

Abrir el sitio: `start http://localhost:5173/`.

### En PowerShell

**Una sola vez — compilar e instalar las dependencias del sitio:**

```powershell
Set-Location C:\modscore1
dotnet build src\channel\Modernization.ChannelApi
npm.cmd --prefix src\web ci --ignore-scripts
Test-Path src\web\node_modules\.bin\vite.cmd
```

El último comando tiene que responder `True`.

**Ventana 1 — capa System:**

```powershell
Set-Location C:\modscore1
dotnet run --no-build --project src\system\Modernization.SystemApi -- --environment Development --urls http://localhost:5101 --DataSource:Provider=File --DataSource:Directory=C:\modscore1\data\local
```

Comprobarla desde otra ventana: `Invoke-RestMethod http://localhost:5101/health`.

**Ventana 2 — capa Channel:**

```powershell
Set-Location C:\modscore1
dotnet run --no-build --project src\channel\Modernization.ChannelApi -- --environment Development --urls http://localhost:5100
```

**Ventana 3 — la SPA:**

```powershell
Set-Location C:\modscore1
npm.cmd --prefix src\web run dev
```

`npm.cmd` y no `npm`: en Windows PowerShell, `npm` resuelve a `npm.ps1`, que la política de
ejecución de scripts suele bloquear. Abrir el sitio: `Start-Process http://localhost:5173/`.

> **No pegues comandos `set "..."` de cmd en PowerShell.** Ahí `set` no define una variable de
> entorno: crea una de PowerShell que la aplicación no ve, sin dar error, y la capa arranca
> como producción. Con los argumentos de arriba no hace falta ninguna variable.

### En Git Bash

```bash
dotnet build src/channel/Modernization.ChannelApi
npm --prefix src/web ci --ignore-scripts
```

```bash
dotnet run --no-build --project src/system/Modernization.SystemApi -- --environment Development --urls http://localhost:5101 --DataSource:Provider=File --DataSource:Directory="$(pwd -W)/data/local"
```

`pwd -W` da la ruta como la entiende Windows (`C:/modscore1`); `$PWD` daría `/c/modscore1`,
que `dotnet` no reconoce.

```bash
dotnet run --no-build --project src/channel/Modernization.ChannelApi -- --environment Development --urls http://localhost:5100
```

```bash
npm --prefix src/web run dev
```

### Sin Vite ni Node: el sitio ya compilado

Para usar la aplicación sin instalar Node, sin ejecutar Vite y sin bajar nada del registro de
npm: solo corre `dotnet.exe`. El sitio compilado **viaja con el código** en la carpeta
`sitio-compilado`, así que también lo trae el ZIP de GitHub (**Code → Download ZIP**). El job
**SPA** del CI comprueba en cada cambio que esa carpeta es byte a byte el build de `src/web`
de ese commit ([ADR 0029](docs/adr/0029-el-sitio-compilado-viaja-con-el-codigo.md)). La
Channel la sirve con `--webroot` y no hay ventana 3. Es el camino C de la
[guía](docs/guia-localhost.md#camino-c--sin-vite-ni-node-windows-cmd-o-powershell).

1. Con el código al día (`git pull` o un ZIP nuevo de `main`), comprobar que está el sitio:
   `dir C:\modscore1\sitio-compilado\index.html` (en PowerShell,
   `Test-Path C:\modscore1\sitio-compilado\index.html` responde `True`).
2. Compilar las dos capas, una sola vez. La línea es la misma en cmd y en PowerShell; tiene que
   terminar con `0 Errores`:

   ```bat
   cd /d C:\modscore1
   dotnet build src\channel\Modernization.ChannelApi -p:UseAppHost=false
   ```

3. Dos ventanas, en este orden. La línea de `dotnet` es la misma en cmd y en PowerShell; en
   PowerShell se entra a la carpeta con `Set-Location C:\modscore1`.

   **Ventana 1 — capa System:**

   ```bat
   cd /d C:\modscore1
   dotnet run --no-build --project src\system\Modernization.SystemApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5101 --DataSource:Provider=File --DataSource:Directory=C:\modscore1\data\local
   ```

   **Ventana 2 — capa Channel, que sirve el sitio:**

   ```bat
   cd /d C:\modscore1
   dotnet run --no-build --project src\channel\Modernization.ChannelApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5100 --webroot C:\modscore1\sitio-compilado
   ```

4. Abrir <http://localhost:5100/> (no el 5173) y **Sign in**.

`-p:UseAppHost=false` hace que quien ejecuta sea `dotnet.exe` y no un `.exe` propio: sirve en
cualquier equipo, y también donde la empresa bloquea programas sin firmar. Si se actualiza el
código, se repite el paso 2: el sitio compilado llega con él, de la misma versión.

**Quien cambie `src/web`** tiene que regenerar la carpeta y versionarla en el mismo commit:
desde `src/web`, `npm run build:sitio-compilado`. Si no, el job SPA del CI falla con
`sitio-compilado/ no corresponde al codigo de src/web`.

### La primera carga

La carpeta `data\local` empieza vacía, así que cada pantalla avisa que no hay datos y lleva a
**Data**. Es lo esperado. Para cargar:

1. En <http://localhost:5173/>, **Sign in**, y la pestaña **Data**.
2. En **Upload files**, arrastrar los ocho CSV de `C:\modscore1\data\dummy` (datos de
   ejemplo, 250 aplicaciones) o los propios. No hace falta renombrarlos: cada archivo se
   reconoce por su encabezado. `eai_master.csv` es obligatorio.
3. **Upload and recalculate**, o antes **Review column mapping** para decidir qué columna de
   cada archivo carga cada campo (ver abajo). La pantalla dice cuántas filas leyó, cuántas
   rechazó y por qué.

Cada carga queda como una versión en **Upload history** y sobrevive al reinicio. Con los CSV
de ejemplo, la aplicación 14381 tiene que dar **79.67**.

### Antes de cargar: el mapping de columnas

Cuando los exports de la organización no traen los nombres de columna del contrato, se
decide antes de publicar qué columna carga cada campo de la base:

1. En **Data → Uploads**, elegir los archivos y pulsar **Review column mapping**. El
   navegador lee **solo la primera línea** de cada archivo —los nombres de las columnas— y
   la manda a revisar; ninguna fila de datos sale del equipo hasta cargar.
2. Por cada archivo aparece su fuente y, por cada campo de la base, la columna que lo
   cargaría: *Same name*, *Known alias*, *As in the last upload* o *Not in this file*, con el
   tipo esperado y las métricas que lo usan. Se cambia con el selector de cada campo; una
   columna del archivo sin campo aparece abajo como *columns of the file that load nothing*.
3. **Upload with this mapping**. La System vuelve a validarlo, guarda el archivo con los
   nombres canónicos en las columnas elegidas y **registra el mapping con la carga**: la
   próxima revisión lo propone solo, si el archivo sigue trayendo esas columnas.

La pestaña **Data → Mapping** muestra lo mismo sin elegir archivos: para cada fuente, la
columna que cargó cada campo en la carga vigente, los nombres con que se reconoce solo y las
métricas que lo usan. El linaje, en cambio, describe lo que ya entró.

### Si se rechazan filas o quedan valores sin dato

La lectura tolera los exports reales, también los de Power BI y Excel en español
([ADR 0028](docs/adr/0028-la-lectura-tolera-los-exports-reales.md)):

- **Un valor ilegible en un campo opcional deja ese campo sin dato y la fila entra**, como hace
  el BRD con un valor que falta. Solo se rechaza la fila sin clave, con un campo obligatorio
  ilegible o con un EAI repetido (queda la última).
- `N/A`, `-`, `NULL` o `(En blanco)` son celdas vacías, no errores.
- Los números se leen en cualquier formato regional («97,67», «1.234», «650,00»), sí/no
  también como `VERDADERO`/`FALSO`, y un EAI escrito como número («14,381», «14381.0») vuelve a
  sus dígitos.
- Se reconocen los campos resumidos («Suma de Kenna_Score», «Sum of …», «Promedio de …»), y el
  nombre exacto del campo gana a un alias: con «MFA» y «Does this Application Utilize MFA?»,
  carga el segundo.
- El export real de Veracode SCA se reconoce con sus nombres: `SVR_DSCR`, `FND_STTS_DSCR`,
  `FND_AGE_DSCR`, `SCAN_TYP_DSCR`, `ROPEN_FND_DSCR`, `CVSSV3_SCOR`, `LIB_VND_NM` y
  `ACCSBLTY_DSCR`. Sus columnas de más (`SCA_ID`, `CMPNT_NM`, `CRT_DT`…) no se usan. Hasta
  128 MB por archivo.

Debajo del detalle por archivo, **Why rows were rejected** y **Values loaded as empty** dicen
qué pasó, agrupado por problema: la columna, qué esperaba, el valor de la primera fila que lo
tuvo, cuántas filas y los números de las primeras, con una pista cuando el valor delata la
causa. Si el EAI Master queda sin filas válidas, la carga se descarta y el portafolio no cambia.

**Sin `APP_ID` en el EAI Master**, la deuda técnica, que viene por APP_ID, se une por el
EAI_Code: en la organización el APP_ID es el mismo código que el EAI. Si el maestro trae la
columna `APP_ID`, manda la columna.

### Para comparar en el tiempo: cuatro trimestres de ejemplo

`data\timeline` trae el mismo portafolio en cuatro cargas trimestrales —Q4 2025, Q1, Q2 y Q3
2026—: las mismas 250 aplicaciones, unas modernizándose y otras envejeciendo. Copiadas a
`data\local` **antes de arrancar la ventana 1**, la pantalla **Code** compara el score, el
Currency Score y el riesgo carga por carga, y el detalle de cada aplicación muestra su propia
evolución.

En cmd:

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

Si la ventana 1 ya estaba corriendo, hay que detenerla (`Ctrl + C`) y volver a arrancarla. Con
datos generados (sin los dos `--DataSource:...`), los cuatro trimestres ya vienen incluidos.
Los regenera `dotnet run --project tools/Modernization.DummyData -- data/dummy`.

### Variantes

Se agregan o quitan argumentos al final de la línea de la ventana que corresponde:

| Variante | Qué cambia |
|---|---|
| **Con el sitio compilado** | Una vez, `npm --prefix src\web run build` (en PowerShell, `npm.cmd`), que deja el sitio en `src\web\dist`. Después se omite la ventana 3 y en la ventana 2 se agrega `--webroot C:\modscore1\src\web\dist`. El sitio queda en <http://localhost:5100/>. La carpeta `dist` se puede compilar en otro equipo y copiar, o descargar ya compilada sin Node ([Sin Vite ni Node](#sin-vite-ni-node-el-sitio-ya-compilado)) |
| **El equipo no deja ejecutar `.exe` sin firmar** | `-p:UseAppHost=false` al final del `dotnet build` y, en cada `dotnet run`, **antes** del `--`. Así quien ejecuta es `dotnet.exe`. Es el camino B de la [guía](docs/guia-localhost.md) |
| **Datos generados, sin cargas** | En la ventana 1, quitar `--DataSource:Provider=File --DataSource:Directory=...`: 250 aplicaciones inventadas en memoria, listas al instante, que se pierden al detener. Data no acepta cargas. Es lo que usan las pruebas e2e |
| **Otra carpeta de datos** | Cambiar la ruta de `--DataSource:Directory`. **Siempre absoluta**: `dotnet run --project` arranca en la carpeta del proyecto, y una ruta relativa terminaría dentro de `src\system\...`. Nunca una carpeta versionada como `data\history`: lo subido quedaría para el próximo commit |
| **Otros puertos** | Cambiar el número en `--urls`; si cambia la System, la Channel necesita `--SystemApi:BaseUrl=http://localhost:<puerto>` |

Ejemplo de la ventana 2 con el sitio compilado y sin ejecutables propios (caminos B y C de la guía):

```bat
dotnet run --no-build --project src\channel\Modernization.ChannelApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5100 --webroot C:\modscore1\src\web\dist
```

- **Desde la app de Claude:** `.claude/launch.json` tiene las tres piezas —`system`,
  `channel` y `web`—. Arrancar `system`, esperar a que responda y después `channel`. La
  `system` también usa el adaptador de archivos sobre `data\local`.

### Si algo falla

| Lo que ves | Qué pasa | Qué hacer |
|---|---|---|
| En Data: `The active adapter is 'Dummy', which serves generated data. Uploading files requires the file or database adapter.` | La ventana 1 arrancó sin `--DataSource:Provider=File --DataSource:Directory=...` | Detener la ventana 1 (`Ctrl + C`) y copiar de nuevo su línea completa |
| `"vite" no se reconoce como un comando interno o externo` | Faltan las dependencias del sitio: no se corrió el `npm ci`, falló, o se corrió en otra carpeta | Desde la raíz del repositorio, el paso «una sola vez» de tu consola: `npm --prefix src\web ci --ignore-scripts` (en PowerShell, `npm.cmd`), y comprobar que existe `src\web\node_modules\.bin\vite.cmd`. Para solo usar la aplicación, [Sin Vite ni Node](#sin-vite-ni-node-el-sitio-ya-compilado) |
| La ventana 3 nunca dice `ready`, o `src\web\node_modules\.bin\esbuild.cmd --version` se queda colgado o dice «acceso denegado» | La política de la empresa bloquea el `esbuild.exe` que Vite ejecuta por dentro, o el antivirus revisa uno a uno los archivos de `node_modules` | [Sin Vite ni Node](#sin-vite-ni-node-el-sitio-ya-compilado): el sitio llega compilado. Para seguir con Vite, pedir a soporte una exclusión para la carpeta del proyecto |
| `npm.ps1 no se puede cargar ... la ejecución de scripts está deshabilitada` | En PowerShell, `npm` es un script que la política bloquea | Escribir `npm.cmd` en lugar de `npm` |
| El `npm ci` termina con `UNABLE_TO_GET_ISSUER_CERT_LOCALLY`, `unable to get local issuer certificate` o `SELF_SIGNED_CERT_IN_CHAIN` | La red de la empresa inspecciona HTTPS y firma con un certificado propio que Windows reconoce y Node no (Node trae su propia lista). Aparece al bajar paquetes nuevos que no están en la caché de npm | En la misma ventana, `set NODE_OPTIONS=--use-system-ca` (PowerShell: `$env:NODE_OPTIONS="--use-system-ca"`) y repetir el `npm ci`: Node suma los certificados de Windows. Requiere Node 22.15+. Si no basta, `NODE_EXTRA_CA_CERTS` con el certificado raíz de la empresa en `.pem`. Nunca `strict-ssl=false` ni `NODE_TLS_REJECT_UNAUTHORIZED=0`. O [Sin Vite ni Node](#sin-vite-ni-node-el-sitio-ya-compilado) |
| El `npm ci` termina con `E403` · `403 Forbidden - GET .../<paquete>-<versión>.tgz` | El registro público entrega esa versión; la bloquea la política de seguridad de la red (muchas bloquean lo publicado hace poco, y las correcciones de seguridad son lo más reciente) | Pedir a soporte que habilite esa versión exacta. No bajar a una anterior: el lock fija la que no tiene vulnerabilidades conocidas. Mientras tanto, [Sin Vite ni Node](#sin-vite-ni-node-el-sitio-ya-compilado) |
| El `npm ci` termina con `ECONNRESET` o `ETIMEDOUT` | La red de la empresa no deja llegar al registro de npm | Pedir a soporte el proxy o el registro interno de npm. Mientras tanto, [Sin Vite ni Node](#sin-vite-ni-node-el-sitio-ya-compilado), que no usa npm |
| Vite dice que la versión de Node no es compatible, o `crypto.hash is not a function` | Node anterior a 20.19 o a 22.12 | Instalar Node 22.12 o superior |
| `'AllowedHosts' debe listar los hosts del servicio (entorno 'Production')` | Faltó `--environment Development` en la línea de esa capa, o se pegó un bloque con `set` en PowerShell | Copiar de nuevo la línea completa, con `-- --environment Development --urls ...` al final |
| El EAI Master queda con 0 filas y todas rechazadas, y la carga se descarta | Desde el 2026-10-02 solo pasa si a todas les falta el EAI (o la columna EAI_Code está vacía); un valor ilegible en otro campo ya no rechaza la fila | Leer **Why rows were rejected**: dice la columna y el valor. Si el EAI viene en otra columna, elegirla con **Review column mapping** |
| Muchas filas en **Values loaded as empty** | Una columna con un formato que no es un número —un rango («100-500»), texto— o un campo cargado desde otra columna | Revisar la columna que nombra; si el campo sale de otra columna, elegirla con **Review column mapping** |

**Detener:** `Ctrl + C` en cada ventana. Si un puerto queda ocupado:
`netstat -ano | findstr :5101` da el número de proceso al final de la línea, y
`taskkill /PID <número> /F` lo cierra (en PowerShell,
`Get-NetTCPConnection -LocalPort 5101 -State Listen` y `Stop-Process -Id <número>`).

La SPA se abre en <http://localhost:5173>. El botón de login usa el esquema de
desarrollo, que **no autentica a nadie**: solo existe para trabajar sin un
proveedor OIDC levantado, y el proceso no arranca si se activa fuera del entorno
`Development`.

## Verificar

```bash
dotnet test ModernizationScore.slnx
```

945 pruebas .NET: 319 del motor, 270 de contratos y 356 de API. Las dos que importan más
reproducen los casos documentados:

- **EAI 14381 = 79,67:** ejemplo trabajado del BRD, derivado desde datos crudos por el
  pipeline completo.
- **EAI 10969 = 79,61:** dashboard Power BI v1.3, métrica por métrica.

`ViewModelContractTests` falla si un modelo de vista de la capa Channel y su tipo
TypeScript difieren.

```bash
npm --prefix src/web test          # 75 pruebas unitarias (Vitest)
npm --prefix src/web run test:e2e  # 56 escenarios: 29 de flujo y 27 de accesibilidad (axe)
```

Las de punta a punta usan el Microsoft Edge instalado (no descargan navegadores) y levantan
las tres piezas con datos dummy, o reutilizan las que ya estén corriendo. Con datos dummy la
carga está apagada, así que la prueba del mapping simula las respuestas de carga; lo que
valida el servidor lo cubren las pruebas de API.

```bash
dotnet list ModernizationScore.slnx package --vulnerable --include-transitive
npm --prefix src/web audit --audit-level=moderate
```

Ambos deben reportar cero. El gate no es decorativo: ya rompió el build una vez,
cuando `Microsoft.AspNetCore.OpenApi` arrastró un `Microsoft.OpenApi` con una
vulnerabilidad alta conocida.

El build compila con **todas** las reglas de seguridad del analizador .NET como
errores, y el restore usa lock files (`packages.lock.json`).

## Seguridad

El resultado del endurecimiento (Fase 08) está en [docs/security](docs/security):
modelo de amenazas, matriz ASVS L2 y registro de hallazgos. Los procedimientos de
operación —rotación de secretos, carga defectuosa, recuperación ante desastres,
incidente— están en [docs/runbooks](docs/runbooks).

El usuario de desarrollo tiene todos los permisos. Para reproducir a uno limitado,
fija sus roles y su alcance al levantar el channel-api:

```bash
Authentication__Development__Roles__0=portfolio.read Authentication__Development__Scopes__0=lob:Individual dotnet run --project src/channel/Modernization.ChannelApi
```

## Consultar y compartir

El sitio está en inglés; la documentación del proyecto sigue en español. Las rutas de las
fases anteriores (`/aplicaciones/…`, `/datos`) redirigen a las nuevas (`/applications/…`,
`/data`), así que un enlace guardado no se rompe.

Los filtros viven en la URL, así que una vista filtrada se comparte copiando el
enlace: `/roadmap?country=Mexico&groupBy=cio`. Son los del dashboard del Modernization
Score: `region`, `country`, `cio`, `cre`, `capability` (capacidad primaria), `status`
(estado de la aplicación), `eai` (código EAI), `critical`, más `lob`, `disposition`,
`roadmap` y `band`. Cada estadística es un filtro —un clic
en «Mexico» acota todas las pantallas— y el filtro sigue puesto al cambiar de menú.
Un filtro solo acota dentro del alcance del usuario: quien abre el enlace ve lo que
su alcance permite, nunca más.

En **Datos**, los exports se suben con cualquier nombre: cada archivo se reconoce por
su encabezado. Si un encabezado encaja en dos fuentes, la pantalla muestra lo que leyó
y deja asignar la fuente a mano.

## El dashboard ejecutivo

El **Dashboard** es la página de inicio (`/`) y la primera pestaña: la vista para
dirección, *Portfolio Health & Risk Intelligence*. El inventario de aplicaciones está en
**Portfolio** (`/portfolio`), y `/dashboard` redirige al inicio conservando los filtros.
Responde una pregunta —dónde se concentra el
riesgo y la oportunidad de modernización, por qué, y qué decisión de arquitectura hace
falta— con cinco indicadores y un panel por cada una de las seis preguntas:

| Panel | Qué muestra |
|---|---|
| Indicadores | Health Score, aplicaciones en riesgo, candidatas a modernizar, exposición de *currency* (EOS/EOL redefinido) y huecos de resiliencia, con su tendencia por carga |
| 1 · Salud | Mapa de calor de capacidad primaria contra región, país, CIO o disposición objetivo; reparto por tramo; tendencia |
| 2 · Prioridad | Matriz criticidad × oportunidad (burbuja = riesgo) y el top 10 |
| 3 · Complejidad | Seis dimensiones en un treemap —las que aún no tienen fuente, punteadas— y cuatro oportunidades de reducción |
| 4 · Riesgo | El mismo mapa de calor midiendo riesgo, el top 5 y el índice de concentración |
| 5 · Obsolescencia | Qué conceptos explican los puntos perdidos y cuánto pesa la vigencia tecnológica |
| 6 · Resiliencia | Nivel por dimensión y por aplicación; las dimensiones sin fuente se dicen como tales |

Cada celda, burbuja, barra o indicador abre el mismo detalle lateral, con los mismos
campos en todos los paneles. El eje del mapa de calor viaja en la URL junto a los
filtros: `/?region=LATAM&axis=cio`.

**Cada panel explica su cálculo.** «How this is calculated», al pie de cada panel del
dashboard y de cada pregunta de Insights, dice qué datos considera, cómo se calcula con los
valores vigentes y qué falta medir. La pestaña **Definitions** (`/definitions`) es el
diccionario de indicadores: para cada pregunta, la definición y la fórmula, los datos, los
valores en vigor, el dueño y el estado del área, la frecuencia y cuánto del portafolio
tiene el dato.

**El Currency Score es el que entrega CMT:** el `Raw_Tech_Score` del Currency Model Detail
Report, de 0 a 100. Una aplicación es *legacy* por debajo de 60 (editable).

**Las definiciones se configuran desde el sitio.** Los 37 parámetros de las seis
preguntas —umbrales, niveles, largo de las listas y los pesos de Insights— viajan con cada
versión de reglas y se editan en **Rules → Portfolio analytics**, con estado (*provisional*
o *agreed*) y dueño por área. Los valores de fábrica están en `contracts/dashboard-v1.json`.
El porqué está en [ADR 0024](docs/adr/0024-el-dashboard-ejecutivo-usa-definiciones-provisionales.md)
y [ADR 0025](docs/adr/0025-las-definiciones-viajan-con-las-reglas.md); las decisiones
abiertas, en [PLAN.md §8.10](PLAN.md) y en PENDING-ITEMS como DD-01 a DD-08. El mismo
análisis está en `GET /api/dashboard`.

## Las seis preguntas del portafolio

La pantalla **Insights** (`/insights`) responde en una sola vista las seis preguntas de
gestión, sobre el mismo recorte que el filtro tiene puesto:

| Pregunta | Qué muestra |
|---|---|
| *How healthy is the portfolio?* | Promedio y mediana, reparto por tramo, cobertura de dato y cuántas críticas están bajo el umbral sano |
| *Which applications should be modernized first?* | Impacto contra esfuerzo: lo que sube el score al ejecutar su roadmap, dividido por las semanas de sus hitos y ponderado por criticidad, usuarios y capacidades |
| *Where should we reduce complexity?* | Misma capacidad repetida en un país, la misma capacidad resuelta distinto en varios países, aplicaciones marcadas `Eliminate` todavía activas, y una capacidad escrita en muchos lenguajes |
| *Where is technology risk concentrated?* | Por LOB, país, CIO o capacidad: flaws críticos y altos, gestión de vulnerabilidades débil, componentes obsoletos y críticas expuestas a internet |
| *What is driving obsolescence?* | Los puntos que pierde el portafolio en cada métrica, con el peso sin dato en columna aparte |
| *Where are the resilience gaps?* | Críticas fuera del alcance de DR o sin monitoreo, score operacional bajo y aplicaciones sin RTO ni RPO |

Cada número viene con su aritmética, y la evidencia de cada fila es una frase con sus
datos, no una etiqueta. El método está en
[ADR 0023](docs/adr/0023-las-seis-preguntas-se-responden-con-lo-que-ya-hay.md).

Dos controles propios viajan en la URL junto a los filtros, así que una vista se comparte
copiando el enlace:

| Parámetro | Valores | Para qué |
|---|---|---|
| `duplicates` | 2 a 5 (por defecto 3) | A partir de cuántas aplicaciones una coincidencia ya es una concentración |
| `riskBy` | `lob`, `country`, `cio`, `capability` | Por qué dimensión se agrupa el riesgo |

Ejemplo: `/insights?country=Mexico&duplicates=2&riskBy=capability`.

El mismo análisis está en `GET /api/insights`, que acepta además `top` (1 a 50) para el
largo de cada lista. Con un alcance limitado, todo se calcula solo sobre lo que ese
alcance deja ver.

**Lo que hoy es indicativo:** el esfuerzo sale de las semanas del roadmap, que son por
roadmap y no por aplicación, y el valor de negocio es un proxy de criticidad, usuarios y
capacidades. Los datos que lo harían real están en
[docs/PENDING-ITEMS.md](docs/PENDING-ITEMS.md) como DQ-01 a DQ-07.

## Código: obsolescencia y riesgo por componentes

La pantalla **Code** (`/code`) mira lo que declaran los repositorios de cada aplicación en
GitHub Enterprise —runtimes, frameworks, servidores de aplicaciones, imágenes de sistema
operativo, bases de datos y bibliotecas— y lo cruza con las fechas de fin de vida y las
alertas de seguridad abiertas ([ADR 0026](docs/adr/0026-el-analisis-de-codigo-llega-de-github-enterprise.md)):

| Panel | Qué muestra |
|---|---|
| Indicadores | Repositorios analizados, aplicaciones con componentes vencidos y por vencer, alertas críticas y altas, y aplicaciones con riesgo de código alto o crítico. Cada uno filtra la tabla de aplicaciones |
| *Where is the stack reaching its end of life?* | Cada producto y ciclo en uso, con su fecha de fin de vida, el estado y el enlace a endoflife.date |
| *Which libraries need attention?* | Bibliotecas obsoletas, desactualizadas o con alertas, y cuántas aplicaciones las usan |
| *Where do the code and the inventory disagree?* | Aplicaciones vigentes según el Currency Score de CMT con componentes vencidos en el código, y al revés |
| *Which applications carry the most code risk?* | El índice de riesgo de código por aplicación; cada una abre sus repositorios y de dónde sale cada punto (`/code?app=14381`) |

- **El fin de vida sale de endoflife.date**, una foto versionada en
  `contracts/eol-catalog-v1.json` (22 productos, 273 ciclos, consultada el 2026-09-26). El
  soporte extendido pagado se muestra, pero no cuenta como soporte.
- **En el tiempo:** *How did score, currency and risk move across uploads?* compara, carga
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
  *Rules → Portfolio analytics*. El índice no cambia el Modernization Score.
- **Hoy los repositorios son de demostración**: se generan del portafolio, deterministas por
  EAI, y la pantalla lo dice en ámbar. Ese proveedor solo arranca en Development
  (`CodeInventory:Provider=Sample`, el valor por defecto ahí); fuera de Development el
  valor por defecto es `None` y la pantalla dice que no hay inventario. La integración real
  —un recolector aparte con una GitHub App de solo lectura— está en el plan (PLAN §8ter) y
  en GH-01 a GH-09 de [docs/PENDING-ITEMS.md](docs/PENDING-ITEMS.md).
- El mismo análisis está en `GET /api/code` (con los filtros de siempre) y
  `GET /api/code/applications/{eai}`.

## Administrar las reglas

La pantalla **Rules** ajusta el cálculo sin recompilar ni desplegar
([ADR 0018](docs/adr/0018-las-reglas-se-publican-como-versiones.md)). Solo aparece para
quien tiene el rol `ruleset.admin` y alcance sobre todo el portafolio. El usuario de
desarrollo lo trae por defecto.

1. **Editar** pesos, bandas, puntajes, palancas, parámetros del roadmap o, en **Portfolio
   analytics**, las definiciones de las seis preguntas. El borrador se guarda en la pestaña
   del navegador.
2. **Simulate impact** recalcula todo el portafolio con las reglas nuevas, sin cambiar nada,
   y muestra cómo se moverían los indicadores del dashboard.
3. **Publish as a new version** pide un motivo y una autenticación de los últimos 10
   minutos. El resto de las réplicas toma la versión nueva en su siguiente comprobación.
4. **Volver atrás:** Versions → Load into draft, y publicar
   ([runbook](docs/runbooks/reglas-defectuosas.md)).

| Variable | Capa | Por defecto | Para qué |
|---|---|---|---|
| `Rules__Directory` | System | `<DataSource__Directory>/.rules` | Dónde guarda las versiones el adaptador de archivos |
| `Rules__RefreshSeconds` | System | 15 | Cada cuánto una réplica comprueba si hay una versión nueva |
| `RulesAdministration__ReauthenticationMinutes` | Channel | 10 | Cuánto vale una autenticación para publicar |
| `RateLimiting__RulesSimulationsPerMinute` | Channel | 10 | Simulaciones por usuario |
| `RateLimiting__RulesPublishesPerWindow` · `RateLimiting__RulesPublishWindowMinutes` | Channel | 5 · 60 | Publicaciones por usuario |

Con el adaptador Dummy las reglas viven en memoria; con base de datos, en la tabla
`rules_version`.

## Comparar reglas y descargar

- **Compare versions** (en Rules) abre `/admin/rules/compare`. Muestra qué cambia entre dos
  versiones, cuánto se mueve el portafolio y, con un código EAI, qué le pasa a esa aplicación
  métrica por métrica. Con **Your draft** como destino, simula el borrador de la pestaña
  antes de publicarlo.
- **Download CSV** en Portfolio y Roadmap descarga lo que se está viendo, con los mismos
  filtros y agrupación. En la comparación descarga cada aplicación antes y después.
- La **tendencia** en Data marca con líneas punteadas cuándo se publicaron reglas. Todos los
  puntos se calculan con las vigentes.
- Cuando se publican reglas nuevas, quien consulta ve un aviso durante 14 días.

| Variable | Capa | Por defecto | Para qué |
|---|---|---|---|
| `RateLimiting__ExportsPerMinute` | Channel | 10 | Descargas por usuario |
| `Portfolio__WarmupSeconds` | System | 15 | Cada cuánto se dejan calculados scores e historia; 0 lo apaga |
| `Uploads__RetainVersions` | System | 0 | Cuántas cargas conserva el adaptador de archivos; 0 conserva todas |

## Autenticación y credencial entre capas

Hoy: OIDC para las personas y un secreto compartido entre las dos capas. Con APIM
([ADR 0022](docs/adr/0022-la-credencial-entre-capas-es-conectable.md)), las dos cosas
cambian por configuración, sin desplegar otra versión. El paso a paso, en el
[runbook](docs/runbooks/paso-a-apim.md).

| Variable | Capa | Por defecto | Para qué |
|---|---|---|---|
| `ServiceToken__Mode` | System | `Static` | Qué acepta: el secreto compartido, `Jwt` de APIM, o `Both` durante el cambio |
| `ServiceToken__Jwt__Authority` · `__Issuer` · `__Audience` | System | — | De dónde salen las llaves y qué emisor y audiencia se exigen |
| `ServiceToken__Jwt__AllowedCallers__0` | System | vacío | Clientes aceptados (`azp`); vacío acepta cualquiera que el emisor autorice |
| `SystemApi__Credential__Mode` | Channel | `Static` | `ClientCredentials` pide un token de vida corta y lo renueva |
| `SystemApi__Credential__TokenEndpoint` · `__ClientId` · `__Scope` | Channel | — | Emisor, cliente y audiencia. El secreto va como archivo `SystemApi__Credential__ClientSecret` |
| `Authentication__Provider` | Channel | `Oidc` | `Apim` usa el flujo de reautenticación de APIM |
| `Authentication__Apim__AuthorizeParameters__*` | Channel | vacío | Parámetros extra de la autorización, por ejemplo `acr_values=mfa` |
| `Authorization__ScopeFromGroups__ClaimType` · `__Map__*` | Channel | vacío | Traduce grupos del IdP a alcance cuando no hay claim propio; el claim manda sobre los grupos |

Los CSV neutralizan textos que una planilla ejecutaría como fórmula, y cada descarga queda
auditada con el evento 4213.

## Respaldo en git

```powershell
powershell -ExecutionPolicy Bypass -File tools/git/backup.ps1 -Message "Descripción del cambio" -Verify
```

Corre las pruebas y los escaneos; se niega a subir capturas del BRD, imágenes, llaves, `.env`
o archivos de más de 100 MB, y nunca fuerza el push. El repositorio es privado y guarda todo
lo creado salvo capturas e imágenes. Ver [docs/runbooks/respaldo-en-git.md](docs/runbooks/respaldo-en-git.md).

## Prueba de carga

```bash
dotnet run --project tools/Modernization.LoadTest -- --duration 15 --concurrency 16
```

Mide la capa System directamente y la Channel con una sesión de desarrollo: pantallas,
las seis preguntas, las descargas y la comparación de reglas. La Channel limita por usuario,
así que para medirla hay que levantarla con los límites **muy** altos —con 100 000 por
minuto la ventana se agota en el primer escenario y el resto mide 429—:

```bash
RateLimiting__RequestsPerMinute=100000000 RateLimiting__ExportsPerMinute=10000000 RateLimiting__RulesSimulationsPerMinute=10000000 dotnet run -c Release --project src/channel/Modernization.ChannelApi
```

La columna 429 del resultado tiene que quedar en cero.

`--scenario <texto>` corre solo los escenarios cuyo nombre lo contiene. Para medir como en
un pod pequeño —la capa System fijada a dos núcleos, simulaciones solas y lecturas mientras
simulan—:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Modernization.LoadTest/medir-en-dos-nucleos.ps1
```

Con la capa System y la herramienta compiladas antes en Release.

## Correr sobre archivos en vez de datos generados

Es como arrancan los comandos de [Levantar el stack](#levantar-el-stack): la ventana 1 lleva
`--DataSource:Provider=File --DataSource:Directory=C:\modscore1\data\local`.

Las rutas van absolutas: `dotnet run --project` arranca con la carpeta del proyecto
como raíz, y una ruta relativa como `.\data\local` termina dentro de
`src\system\Modernization.SystemApi` —el portafolio aparece sin cargas, sin ningún error—.

Con el adaptador de archivos, la pantalla **Data** permite subir los CSV y
recalcular. Cada carga se publica como una versión consultable, y la evolución del
score aparece en esa misma pantalla. Las reglas publicadas se guardan en `.rules\`, dentro
de la misma carpeta.

En el primer arranque no hay ninguna carga: las pantallas lo dicen y llevan a
**Data**, que es la única que funciona sin datos. Los ocho CSV de `data\dummy` sirven para la
primera (ver [La primera carga](#la-primera-carga)).

`data\history` tiene dos cargas versionadas en el repositorio, de la verificación de la fase 8.
Sirven para mirar la evolución del score sin cargar nada, pero no como carpeta de trabajo:
lo que se suba ahí quedaría para el próximo commit.

## Correr sobre una base de datos

```bash
DataSource__Provider=Database Database__Provider=Sqlite \
  ConnectionStrings__Modernization="Data Source=$PWD/data/modernization.db" \
  dotnet run --project src/system/Modernization.SystemApi
```

El esquema se aplica solo al arrancar. La pantalla **Cargas** funciona igual que con
archivos: cada carga es una transacción en vez de un directorio, y
`Database__RetainVersions` conserva las N más recientes.

El motor de despliegue es una decisión abierta (RFC-07). El modelo es agnóstico y se
verifica contra SQLite, que es una base real; añadir otro motor es un paquete y un
juego de migraciones:

```bash
dotnet ef migrations add <Nombre> --project src/system/Modernization.Sources.Database
```

## Regenerar los datos dummy

```bash
dotnet run --project tools/Modernization.DummyData -- data/dummy
```

Genera 250 aplicaciones deterministas y las exporta como ocho CSV con los nombres
de columna reales de cada archivo observado. Devuelve código de error si el EAI de
referencia deja de dar 79,67.

## Estructura

```
contracts/ruleset-v1.3.json     18 métricas: pesos, bandas y procedencia — reglas como datos
contracts/dashboard-v1.json     valores de fábrica de las definiciones de las seis preguntas
data/dummy/                     los ocho CSV generados
data/timeline/                  el mismo portafolio en cuatro cargas trimestrales
src/shared/                     dominio, motor de scoring, what-if, roadmap, insights y dashboard, reglas, seguridad
src/system/                     capa System: reglas de negocio + fuentes
src/channel/                    capa Channel: BFF, composición por pantalla y exportaciones
src/web/                        SPA React 19 · pruebas unitarias en src/ · e2e/ con Playwright
tests/                          golden tests, contract tests, E2E por HTTP y seguridad
tools/                          datos dummy, catálogo de fin de vida, prueba de carga y respaldo en git
docs/                           ADR, seguridad, runbooks, índice del BRD y PENDING-ITEMS.md
deploy/                         imágenes y manifiestos del cluster
HANDOFF.md                      traspaso completo del contexto
```

## Estado

| Fase | Estado |
|---|---|
| 00 Fundaciones | entregada |
| 01 Motor de scoring | entregada |
| 02 Datos dummy | entregada |
| 03 Capas Channel y System | entregada |
| 04 SPA base | entregada |
| 05 What-If | entregada |
| 06 Roadmap | entregada |
| 07 Adaptador de archivos | entregada |
| 08 Endurecimiento | entregada |
| 09 Adaptador de base de datos | entregada |
| 10 Experiencia de consulta | entregada |
| 11 Sitio en inglés | entregada |
| 12 Administración de reglas | entregada |
| 13 Revisión y remediación | entregada |
| 14 Verificación de seguridad (ASVS L2) | en curso: hecho lo que no necesita un entorno real (PLAN §8.7) |
| 15 Autenticación con APIM | en curso: código listo y probado con dobles; falta el emisor real (PLAN §8.8) |
| Pantalla 09 — las seis preguntas | entregada (PLAN §8.9) |
| Dashboard ejecutivo | D1, D2 y D3 entregadas: página de inicio, diccionario y definiciones editables desde el portal; D4 (fuentes nuevas) planificada (PLAN §8.10) |
| 16 Escala y operación | planificada |
| 17 Hallazgos de infraestructura (H-16, H-17, H-20, H-21) | planificada, al final por decisión |

Los supuestos abiertos están en la sección 10 de [PLAN.md](PLAN.md) como RFC-01 a
RFC-17; todos tienen un default implementado y configurable. Los pendientes para revisar
con el equipo, con contexto, opciones y criterio de aceptación, están en
[docs/PENDING-ITEMS.md](docs/PENDING-ITEMS.md). Para retomar el proyecto, empezar por
[HANDOFF.md](HANDOFF.md).
