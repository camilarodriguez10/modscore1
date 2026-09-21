# Modernization Score — paquete portable de prueba

Paso intermedio antes de publicar: permite probar la aplicación completa en otro equipo
Windows sin instalar Visual Studio, Node ni (en la variante autocontenida) .NET.

> **No es un despliegue.** Corre en el entorno Development con la autenticación de
> desarrollo: quien llegue al puerto entra como administrador. Por eso las dos capas
> escuchan solo en `127.0.0.1`. No usar con datos reales ni exponer en una red.

> Si quien lo va a usar no conoce el proyecto, el paso a paso completo —con lo que tiene que
> ver en pantalla en cada momento y los problemas frecuentes— viaja **dentro del paquete**
> como `GUIA-PASO-A-PASO.txt`, y en el repositorio está en
> [docs/guia-localhost.md](../../docs/guia-localhost.md).

Todos los comandos de abajo suponen que el paquete queda en `C:\modscore`. Si lo
pones en otro lado, cambia esa ruta en cada comando. Cada bloque se copia completo y se pega
en la ventana indicada.

> **Por qué `.\` delante de cada programa.** Algunos equipos corporativos tienen activada la
> política `NoDefaultCurrentDirectoryInExePath`: con ella, `cmd` no ejecuta un programa de la
> carpeta actual si no se le antepone `.\`, y responde «no se reconoce como un comando
> interno o externo». Con `.\` funciona en cualquier equipo, así que los comandos lo llevan
> siempre.

## 1. Descomprimir y comprobar el paquete

Desde la carpeta donde está el `.zip` (por ejemplo, Descargas).

**cmd:**

```bat
cd /d %USERPROFILE%\Downloads
for %f in (modscore-portable-*.zip) do certutil -hashfile "%f" SHA256
mkdir C:\modscore
for %f in (modscore-portable-*.zip) do tar -xf "%f" -C C:\modscore
dir C:\modscore
```

**PowerShell:**

```powershell
Set-Location $env:USERPROFILE\Downloads
Get-FileHash -Algorithm SHA256 .\modscore-portable-*.zip
Expand-Archive -Path (Get-Item .\modscore-portable-*.zip).FullName -DestinationPath C:\modscore
Get-ChildItem C:\modscore
```

El SHA-256 tiene que coincidir con el que acompaña al `.zip` (en el repositorio, en
`HANDOFF.md`). La carpeta tiene que mostrar `channel`, `system`, `samples`, `start.cmd`,
`start.ps1`, `README.txt` y `GUIA-PASO-A-PASO.txt`. No descomprimir dentro de una carpeta
sincronizada con OneDrive.

## 2. Arrancar con el script (lo normal)

El script levanta las dos capas, genera un token de servicio nuevo en cada arranque y abre
el navegador en <http://localhost:5100/>. `Sign in` entra con el usuario de desarrollo, que
tiene todos los permisos. **Para detener: Enter en esa misma ventana.**

**cmd:**

```bat
cd /d C:\modscore
.\start.cmd
```

**PowerShell:**

```powershell
Set-Location C:\modscore
powershell -NoProfile -ExecutionPolicy Bypass -File .\start.ps1
```

`-ExecutionPolicy Bypass` vale solo para ese proceso: no cambia la política del equipo.
También funciona el doble clic en `start.cmd` desde el Explorador.

### Opciones

Se agregan al final de la línea, igual en cmd (`.\start.cmd ...`) y en PowerShell
(`... -File .\start.ps1 ...`):

| Opción | Qué hace |
|---|---|
| *(ninguna)* | 250 aplicaciones generadas en memoria (se pierden al detener) |
| `-Data File` | Carga de archivos: subir los CSV de `samples\` desde la pantalla **Data**. Las cargas y las reglas publicadas quedan en `data\` |
| `-Port 5200` | Otro puerto (la capa System usa el siguiente) |
| `-NoBrowser` | No abre el navegador |

Ejemplo en cmd: `.\start.cmd -Data File -NoBrowser`. En PowerShell:
`powershell -NoProfile -ExecutionPolicy Bypass -File .\start.ps1 -Data File -NoBrowser`.

## 3. Arrancar sin el script: los dos programas a mano

Sirve si el equipo no deja correr scripts pero sí los `.exe` del paquete, o para ver los
mensajes de cada capa por separado. Son **dos ventanas**, en este orden, y cada una se
deja abierta mientras se usa la aplicación.

El entorno, el puerto y la dirección de la capa System van **como argumentos del programa**,
no como variables. Así la línea del programa es **idéntica en cmd y en PowerShell**; solo
cambia la forma de entrar a la carpeta.

> **No pegues comandos `set "..."` en PowerShell.** En PowerShell, `set` no define una
> variable de entorno: crea una variable de PowerShell que el programa no ve, sin dar error.
> La capa arranca entonces como producción y se detiene con `'AllowedHosts' debe listar los
> hosts del servicio (entorno 'Production')`. Con los argumentos de abajo eso no pasa.

A mano, las capas usan el token de desarrollo de sus `appsettings.Development.json` en lugar
de uno nuevo por arranque. Da lo mismo en un equipo de prueba: las dos escuchan solo en
`127.0.0.1`.

**Ventana 1 — capa System.** En cmd:

```bat
cd /d C:\modscore\system
.\Modernization.SystemApi.exe --environment Development --urls http://127.0.0.1:5101
```

En PowerShell:

```powershell
Set-Location C:\modscore\system
.\Modernization.SystemApi.exe --environment Development --urls http://127.0.0.1:5101
```

Tiene que aparecer `Now listening on: http://127.0.0.1:5101` y `Hosting environment:
Development`. Antes de abrir la ventana 2, comprueba desde otra ventana que responde: en cmd,
`curl http://127.0.0.1:5101/health`; en PowerShell, `Invoke-RestMethod http://127.0.0.1:5101/health`.
Tiene que decir `healthy`.

**Ventana 2 — capa Channel, que además sirve el sitio.** En cmd:

```bat
cd /d C:\modscore\channel
.\Modernization.ChannelApi.exe --environment Development --urls http://127.0.0.1:5100 --SystemApi:BaseUrl=http://127.0.0.1:5101
```

En PowerShell:

```powershell
Set-Location C:\modscore\channel
.\Modernization.ChannelApi.exe --environment Development --urls http://127.0.0.1:5100 --SystemApi:BaseUrl=http://127.0.0.1:5101
```

Cuando aparezca `Now listening on: http://127.0.0.1:5100`, abre el sitio: `start http://localhost:5100/`
en cmd, o `Start-Process http://localhost:5100/` en PowerShell.

| Argumento | Para qué |
|---|---|
| `--environment Development` | Arranca como entorno de desarrollo: lee `appsettings.Development.json` y habilita el usuario de desarrollo. Sin esto arranca como producción y se detiene a propósito |
| `--urls http://127.0.0.1:5101` | El puerto y la interfaz donde escucha. Sin esto escucharía en el 5000 |
| `--SystemApi:BaseUrl=http://127.0.0.1:5101` | Dónde encuentra la Channel a la System |

### Con datos de archivos en lugar de generados

Solo cambia la **ventana 1**: dos argumentos más. La carpeta `data` guarda las cargas y las
reglas publicadas; se crea una vez.

En cmd:

```bat
if not exist C:\modscore\data mkdir C:\modscore\data
cd /d C:\modscore\system
.\Modernization.SystemApi.exe --environment Development --urls http://127.0.0.1:5101 --DataSource:Provider=File --DataSource:Directory=C:\modscore\data
```

En PowerShell:

```powershell
New-Item -ItemType Directory -Force C:\modscore\data | Out-Null
Set-Location C:\modscore\system
.\Modernization.SystemApi.exe --environment Development --urls http://127.0.0.1:5101 --DataSource:Provider=File --DataSource:Directory=C:\modscore\data
```

Después, en el sitio, **Data → Upload files**: elegir los ocho CSV de `C:\modscore\samples` y
**Upload and recalculate**.

### Con otros puertos

Si el 5100 o el 5101 están ocupados, cambia los números en los tres argumentos que los nombran:
`--urls` de la ventana 1, y `--urls` y `--SystemApi:BaseUrl` de la ventana 2. Por ejemplo,
5201 para la System y 5200 para la Channel.

## 4. Detener

- **Con el script:** Enter en su ventana. Cierra las dos capas y borra el token temporal.
- **A mano:** `Ctrl + C` en cada ventana, primero la Channel y después la System.

Si una capa quedó abierta y el puerto sigue ocupado:

**cmd:**

```bat
netstat -ano | findstr :5100
netstat -ano | findstr :5101
taskkill /IM Modernization.ChannelApi.exe /F
taskkill /IM Modernization.SystemApi.exe /F
```

**PowerShell:**

```powershell
Get-NetTCPConnection -LocalPort 5100,5101 -State Listen -ErrorAction SilentlyContinue
Get-Process -Name 'Modernization.*' -ErrorAction SilentlyContinue | Stop-Process -Force
```

## 5. Si la empresa bloquea los `.exe`

Windows puede advertir que los ejecutables no están firmados (SmartScreen): es un aviso, y
el paquete de prueba no se firma (la firma de binarios e imágenes es parte del hallazgo
H-17). Si en cambio los **bloquea** —no abren—, este paquete no sirve en ese equipo tal cual.
El camino C de `GUIA-PASO-A-PASO.txt` levanta la aplicación desde el código solo con
`dotnet.exe`, y de este `.zip` usa únicamente la carpeta `channel\wwwroot`, que son archivos
estáticos. Necesita el .NET SDK 10 y el repositorio (en el ejemplo, en `C:\modscore1`).

Las líneas de `dotnet` son idénticas en cmd y en PowerShell. Para entrar a la carpeta: en
cmd, `cd /d C:\modscore1`; en PowerShell, `Set-Location C:\modscore1`.

**Compilar una vez:**

```bat
cd /d C:\modscore1
dotnet build src\channel\Modernization.ChannelApi -p:UseAppHost=false
```

**Ventana 1 — System:**

```bat
cd /d C:\modscore1
dotnet run --no-build --project src\system\Modernization.SystemApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5101
```

**Ventana 2 — Channel, sirviendo el sitio del paquete:**

```bat
cd /d C:\modscore1
dotnet run --no-build --project src\channel\Modernization.ChannelApi -p:UseAppHost=false -- --environment Development --urls http://localhost:5100 --webroot C:\modscore\channel\wwwroot
```

Luego se abre <http://localhost:5100/>. Lo que va antes de `--` es para `dotnet`; lo que va
después, para la aplicación. `-p:UseAppHost=false` hace que quien ejecuta sea `dotnet.exe` y no
un `.exe` propio del proyecto; va también en `dotnet run`, porque sin él `--no-build` busca ese
`.exe`. `--webroot` le dice a la Channel dónde están los archivos del sitio.

## Qué probar

- **Dashboard** (la página de inicio): cinco indicadores y seis paneles; cambiar el eje del
  mapa de calor, hacer clic en una celda, una burbuja o un indicador para abrir el detalle, y
  abrir *How this is calculated* al pie de un panel.
- **Definitions**: el diccionario de indicadores, con los valores en vigor y la cobertura de
  datos.
- **Rules → Portfolio analytics**: cambiar un umbral, simular y ver cómo se moverían los
  indicadores del dashboard.
- **Insights**: las seis preguntas del portafolio, con los mismos filtros.
- **Portfolio**: filtros desde los gráficos, navegación con el filtro puesto, **Download CSV**.
- **Cálculo** de una aplicación: <http://localhost:5100/applications/14381/calculation> debe
  dar **79,67**.
- **What-if** y **plan futuro** de una aplicación.
- **Roadmap** por dimensión y su **Download CSV**.
- **Data**: con datos de archivos, subir los ocho CSV de `samples\`; la tendencia aparece
  desde la segunda carga y marca los cambios de reglas.
- **Rules**: cambiar un peso, simular, **Compare your draft** con una aplicación, publicar.
  Tras publicar aparece el aviso de reglas nuevas.

## Seguridad del paquete

- Las dos capas escuchan solo en la interfaz local.
- Con el script, el token de servicio entre capas se genera en cada arranque, viaja como
  archivo en una carpeta temporal (igual que con el gestor de secretos) y se borra al
  detener. A mano, se usa el token de desarrollo de `appsettings.Development.json`.
- La SPA se sirve con la misma CSP estricta que en despliegue y sin source maps.
- Sin símbolos de depuración en el paquete.

## Requisitos del equipo

| Variante | Requisito | Tamaño aproximado |
|---|---|---|
| `selfcontained` | Windows 10/11 x64 | 90–110 MB descomprimido |
| `frameworkdependent` | Runtime de ASP.NET Core 10 instalado | 15–25 MB descomprimido |

Puertos libres: 5100 y 5101 (o los que indique `-Port`). `tar` y `curl` vienen con Windows
10 (1803) y 11.

## Generar el paquete

Desde la raíz del repositorio, en cmd o en PowerShell:

```bat
powershell -NoProfile -ExecutionPolicy Bypass -File tools\portable\build-portable.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\portable\build-portable.ps1 -Variant FrameworkDependent
```

El `.zip` y su SHA-256 quedan en `artifacts/`. El último `.zip` se versiona en el repositorio;
la carpeta descomprimida no. Antes de confirmar uno nuevo, borrar el anterior: cada paquete
queda para siempre en la historia y GitHub rechaza archivos de más de 100 MB.
