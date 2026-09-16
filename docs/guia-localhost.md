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
| **Sitio (SPA)** | Lo que ves en pantalla | `5173` (solo en el camino B) |

Hay **dos caminos**. Elige uno:

| | Camino A · paquete portable | Camino B · desde el código |
|---|---|---|
| Para quién | Alguien que solo quiere **usar** la aplicación | Alguien que va a **modificarla** |
| Sistema | Windows 10/11 de 64 bits | Windows, macOS o Linux |
| Hay que instalar | **Nada** | .NET 10 y Node.js |
| Cuánto toma | 5 minutos | 20 minutos la primera vez |
| Se abre en | `http://localhost:5100` | `http://localhost:5173` |

Si dudas, empieza por el **camino A**.

---

## Camino A · el paquete portable (Windows, sin instalar nada)

### Paso 1 · consigue el `.zip`

Está en el repositorio, en la carpeta `artifacts/`, con un nombre como
`modscore-portable-win-x64-selfcontained-AAAAMMDD-HHMM.zip` (unos 93 MB). Cópialo al otro
equipo con un pendrive, una carpeta compartida o como prefieras.

**Comprueba que llegó completo** (opcional pero recomendado). Abre PowerShell donde está el
archivo y escribe:

```powershell
Get-FileHash -Algorithm SHA256 .\modscore-portable-win-x64-selfcontained-20260916-1524.zip
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

Doble clic en **`start.cmd`**.

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

Tres comprobaciones de treinta segundos:

1. En **Portfolio** tiene que haber una tabla con unas 250 aplicaciones.
2. Abre `http://localhost:5100/applications/14381/calculation`. El score de esa aplicación
   tiene que ser exactamente **79.67**. Es el ejemplo trabajado del documento de negocio:
   si ese número sale, el cálculo está bien de punta a punta.
3. Abre **Insights** en el menú. Tienen que aparecer las seis preguntas del portafolio, la
   primera con el promedio y la mediana.

### Paso 6 · para detenerlo

Vuelve a la ventana negra y pulsa **Enter**. Verás `Detenido.`, y con eso se cierran las dos
capas y se borra el token temporal que usan para hablar entre ellas.

> No cierres la ventana con la ✕ si puedes evitarlo: al cerrarla sin más pueden quedar los
> dos procesos vivos. Si pasa, ver [Problemas frecuentes](#problemas-frecuentes).

### Opciones del arranque

Abre PowerShell en `C:\modscore` y usa `start.ps1` en vez del doble clic:

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
| **Node.js** | 22 o superior | <https://nodejs.org> |

Comprueba que quedaron instaladas (cierra y vuelve a abrir la terminal después de
instalar):

```bash
dotnet --version
```

```bash
node --version
```

El primero tiene que decir `10.x.x` y el segundo `v22.x` o superior. Si dicen «no se
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

Una sola vez, para bajar las dependencias del frontend:

```bash
npm --prefix src/web install
```

Tarda uno o dos minutos. Al terminar no debe haber ningún `vulnerabilities` distinto de
cero.

### Paso 4 · levanta las tres piezas

Necesitas **tres terminales abiertas a la vez**, en la carpeta del proyecto. En cada una,
un comando, y **en este orden**:

**Terminal 1 — capa System:**

```bash
dotnet run --project src/system/Modernization.SystemApi
```

Espera a ver `Now listening on: http://localhost:5101`.

**Terminal 2 — capa Channel:**

```bash
dotnet run --project src/channel/Modernization.ChannelApi
```

Espera a ver `Now listening on: http://localhost:5100`.

**Terminal 3 — el sitio:**

```bash
npm --prefix src/web run dev
```

Espera a ver `Local: http://localhost:5173/`.

La primera vez, los dos primeros comandos tardan porque compilan el proyecto entero. Las
siguientes veces arrancan en segundos.

### Paso 5 · entra y comprueba

Abre <http://localhost:5173> y pulsa **Sign in**. Luego, las mismas tres comprobaciones del
camino A:

1. Portfolio con unas 250 aplicaciones.
2. `http://localhost:5173/applications/14381/calculation` da **79.67**.
3. **Insights** muestra las seis preguntas.

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

Deben terminar en verde: 694 pruebas .NET y 39 de la interfaz.

---

## Probar con archivos en vez de datos generados

Por defecto la aplicación inventa 250 aplicaciones en memoria, que se pierden al detenerla.
Para probar la carga de archivos de verdad:

**Camino A:**

```powershell
.\start.ps1 -Data File
```

**Camino B** (en la terminal de la capa System, en lugar del comando del paso 4):

```bash
DataSource__Provider=File DataSource__Directory="$PWD/data/history" dotnet run --project src/system/Modernization.SystemApi
```

En Windows con PowerShell:

```powershell
$env:DataSource__Provider='File'; $env:DataSource__Directory="$PWD\data\history"; dotnet run --project src/system/Modernization.SystemApi
```

> **La ruta tiene que ser absoluta.** `dotnet run --project` arranca con la carpeta del
> proyecto como raíz, así que una ruta relativa apunta a un directorio que no existe: el
> portafolio aparece vacío, sin ningún error que lo explique.

Luego, en el sitio:

1. Entra en **Data**.
2. Arrastra los ocho CSV (los de `samples\` en el paquete portable, o los de `data/dummy/`
   en el repositorio). **No hace falta renombrarlos**: cada archivo se reconoce por su
   encabezado.
3. Pulsa publicar. La pantalla dice cuántas filas leyó, cuántas rechazó y por qué.

La primera vez que arranques con archivos y sin ninguna carga, las pantallas dirán que no
hay datos y te llevarán a **Data**. Es lo esperado, no un error.

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
| La consola se cierra sola al instante | Doble clic sobre `start.ps1` en vez de `start.cmd` | Usa `start.cmd`, o abre PowerShell y ejecuta `.\start.ps1` |
| `No se puede cargar el archivo start.ps1 ... deshabilitada la ejecución de scripts` | Política de ejecución de PowerShell | `powershell -ExecutionPolicy Bypass -File .\start.ps1` |
| «Windows protegió su PC» | SmartScreen: los binarios no están firmados | Más información → Ejecutar de todas formas |
| `Failed to bind to address ... address already in use` | Otro programa ocupa 5100, 5101 o 5173 | Camino A: `.\start.ps1 -Port 5200`. Camino B: cierra lo que use el puerto (ver fila siguiente) |
| Necesitas saber **quién** ocupa un puerto | — | Windows: `netstat -ano \| findstr :5100` y luego `tasklist \| findstr <PID>`. macOS/Linux: `lsof -i :5100` |
| El sitio carga pero todo sale vacío o con error | La capa Channel está arriba, la System no | Mira la terminal 1: tiene que decir `Now listening on: http://localhost:5101` |
| El sitio no carga («no se puede acceder a este sitio») | La capa Channel no está arriba | Mira la terminal 2, y arráncala después de la System |
| `502` o `500` al entrar | Las dos capas no se están reconociendo | Deténlas todas y arráncalas en orden: System, Channel, sitio |
| Ves la página de login y `Sign in` no hace nada | El proceso no está en entorno `Development` | Camino B: usa los comandos tal cual están aquí, sin `ASPNETCORE_ENVIRONMENT=Production` |
| Al ejecutar un `.exe` a mano dice que no lo encuentra | Windows no busca en la carpeta actual | Escribe `.\Modernization.SystemApi.exe`, y hazlo **desde la carpeta de esa capa** |
| La ventana negra se cerró y los puertos siguen ocupados | Los procesos quedaron vivos | Windows: `taskkill /IM Modernization.SystemApi.exe /F` y lo mismo con `Modernization.ChannelApi.exe` |
| Página en blanco tras actualizar el código | Faltan dependencias nuevas del sitio | `npm --prefix src/web install` otra vez |
| `dotnet test` falla con «el archivo está siendo usado por otro proceso» | La aplicación está corriendo | Detén las tres piezas antes de compilar o probar |
| Todo iba bien y dejó de arrancar tras mover la carpeta | Ruta dentro de OneDrive o demasiado larga | Muévela a algo como `C:\modscore` |

---

## Cómo dejar el equipo como estaba

1. Detén la aplicación (Enter en la consola, o Ctrl + C en las tres terminales).
2. Borra la carpeta donde descomprimiste el paquete (camino A) o donde clonaste el
   repositorio (camino B).
3. Si arrancaste con `-Data File`, ahí dentro está la carpeta `data\` con lo que hayas
   cargado: se va con la carpeta.

No queda nada instalado ni registrado en el sistema: el paquete portable no toca el
registro de Windows ni instala servicios.

---

## Checklist

- [ ] Las tres piezas arriba (o las dos del paquete portable).
- [ ] `Sign in` entra sin pedir contraseña.
- [ ] Portfolio muestra ~250 aplicaciones.
- [ ] `/applications/14381/calculation` da **79.67**.
- [ ] **Insights** responde las seis preguntas.
- [ ] Al detener, ninguna ventana queda abierta y los puertos quedan libres.

---

## Para seguir

- [README.md](../README.md) — qué hace la plataforma y cómo se verifica.
- [tools/portable/README.md](../tools/portable/README.md) — detalle del paquete portable y
  cómo generar uno nuevo.
- [HANDOFF.md](../HANDOFF.md) — el contexto completo del proyecto.
- [docs/runbooks](runbooks) — procedimientos de operación (rotación de secretos, carga
  defectuosa, recuperación, reglas defectuosas).
