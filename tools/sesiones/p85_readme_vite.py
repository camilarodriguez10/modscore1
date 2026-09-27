from patchlib import patch

patch("README.md", [
("""- La primera vez, instalar las dependencias del sitio: `npm --prefix src/web ci --ignore-scripts`
  (en PowerShell, `npm.cmd` en lugar de `npm`).

Los ejemplos suponen el repositorio en `C:\\modscore1`; cambia esa ruta si está en otro lado.""",
"""- **La primera vez, instalar las dependencias del sitio** (el paso «una sola vez» de cada
  consola, abajo). Sin ellas, la ventana 3 falla con `"vite" no se reconoce como un comando
  interno o externo`. Hace falta **Node.js 22.12 o superior** (o 20.19+): `node --version`.
- **¿Solo vas a usar la aplicación, no a cambiar la interfaz?** No hace falta Node ni la
  ventana 3: la Channel sirve el sitio ya compilado del paquete portable (ver
  [Variantes](#variantes), «Sin Node»).

Los ejemplos suponen el repositorio en `C:\\modscore1`; cambia esa ruta si está en otro lado."""),
# cmd
("""### En cmd

**Compilar una vez:**

```bat
cd /d C:\\modscore1
dotnet build src\\channel\\Modernization.ChannelApi
```

Tiene que terminar con `0 Errores` (o `0 Error(s)`).
""",
"""### En cmd

**Una sola vez — compilar e instalar las dependencias del sitio:**

```bat
cd /d C:\\modscore1
dotnet build src\\channel\\Modernization.ChannelApi
npm --prefix src\\web ci --ignore-scripts
dir src\\web\\node_modules\\.bin\\vite.cmd
```

El `dotnet build` tiene que terminar con `0 Errores` (o `0 Error(s)`), el `npm ci` con
`found 0 vulnerabilities`, y el `dir` tiene que **encontrar** `vite.cmd`. Si dice «No se
encuentra el archivo», la instalación no terminó: lee el error del `npm ci` (ver
[Si algo falla](#si-algo-falla)). Compilar hay que repetirlo cuando cambia el código;
instalar, cuando cambia `src\\web\\package-lock.json`.
"""),
# PowerShell
("""### En PowerShell

**Compilar una vez:**

```powershell
Set-Location C:\\modscore1
dotnet build src\\channel\\Modernization.ChannelApi
```
""",
"""### En PowerShell

**Una sola vez — compilar e instalar las dependencias del sitio:**

```powershell
Set-Location C:\\modscore1
dotnet build src\\channel\\Modernization.ChannelApi
npm.cmd --prefix src\\web ci --ignore-scripts
Test-Path src\\web\\node_modules\\.bin\\vite.cmd
```

El último comando tiene que responder `True`.
"""),
# Git Bash
("""### En Git Bash

```bash
dotnet build src/channel/Modernization.ChannelApi
```
""",
"""### En Git Bash

```bash
dotnet build src/channel/Modernization.ChannelApi
npm --prefix src/web ci --ignore-scripts
```
"""),
# Si algo falla
("""**Detener:** `Ctrl + C` en cada ventana. Si un puerto queda ocupado:""",
"""### Si algo falla

| Lo que ves | Qué pasa | Qué hacer |
|---|---|---|
| `"vite" no se reconoce como un comando interno o externo` | Faltan las dependencias del sitio: no se corrió el `npm ci`, falló, o se corrió en otra carpeta | Desde la raíz del repositorio, el paso «una sola vez» de tu consola: `npm --prefix src\\web ci --ignore-scripts` (en PowerShell, `npm.cmd`), y comprobar que existe `src\\web\\node_modules\\.bin\\vite.cmd` |
| `npm.ps1 no se puede cargar ... la ejecución de scripts está deshabilitada` | En PowerShell, `npm` es un script que la política bloquea | Escribir `npm.cmd` en lugar de `npm` |
| El `npm ci` termina con `ECONNRESET`, `ETIMEDOUT`, `E403`, `self-signed certificate` o `unable to get local issuer certificate` | La red de la empresa no deja llegar al registro de npm | Pedir a soporte el proxy o el registro interno de npm. Mientras tanto, usar la variante **sin Node** |
| Vite dice que la versión de Node no es compatible, o `crypto.hash is not a function` | Node anterior a 20.19 o a 22.12 | Instalar Node 22.12 o superior |
| `'AllowedHosts' debe listar los hosts del servicio (entorno 'Production')` | Faltó `--environment Development` en la línea de esa capa, o se pegó un bloque con `set` en PowerShell | Copiar de nuevo la línea completa, con `-- --environment Development --urls ...` al final |

**Detener:** `Ctrl + C` en cada ventana. Si un puerto queda ocupado:"""),
])
