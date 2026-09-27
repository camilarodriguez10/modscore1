from patchlib import patch

patch(r"docs\guia-localhost.md", [
("""| **Node.js** | 22 o superior | <https://nodejs.org> |""",
 """| **Node.js** | 22.12 o superior (o 20.19+) | <https://nodejs.org> |"""),
("""El primero tiene que decir `10.x.x` y el segundo `v22.x` o superior. Si dicen «no se
reconoce el comando», la instalación no agregó la herramienta al `PATH`: reinicia la
terminal, y si sigue igual, reinicia el equipo.""",
"""El primero tiene que decir `10.x.x` y el segundo `v22.12` o superior (sirve también `v20.19`
o superior de la serie 20). Con un Node más viejo el sitio no arranca. Si dicen «no se
reconoce el comando», la instalación no agregó la herramienta al `PATH`: reinicia la
terminal, y si sigue igual, reinicia el equipo."""),
("""### Paso 3 · prepara el sitio

Una sola vez, para bajar las dependencias del frontend:

```bash
npm --prefix src/web install
```

Tarda uno o dos minutos. Al terminar no debe haber ningún `vulnerabilities` distinto de
cero.""",
"""### Paso 3 · prepara el sitio

Una sola vez, desde la carpeta del proyecto, para bajar las dependencias del frontend. **Sin
este paso, la terminal 3 falla con `"vite" no se reconoce como un comando interno o
externo`.**

En `cmd`:

```bat
npm --prefix src\\web ci --ignore-scripts
dir src\\web\\node_modules\\.bin\\vite.cmd
```

En PowerShell (con `npm.cmd`: `npm` a secas es un script que la política suele bloquear):

```powershell
npm.cmd --prefix src\\web ci --ignore-scripts
Test-Path src\\web\\node_modules\\.bin\\vite.cmd
```

En macOS o Linux:

```bash
npm --prefix src/web ci --ignore-scripts
```

Tarda uno o dos minutos. Tiene que terminar con `found 0 vulnerabilities`, y la segunda línea
tiene que **encontrar** `vite.cmd` (en PowerShell, responder `True`). Si no lo encuentra, la
instalación falló: lee el error y busca en [Problemas frecuentes](#problemas-frecuentes).
`ci` instala exactamente las versiones del archivo de bloqueo, las mismas que se auditaron;
`--ignore-scripts` impide que un paquete ejecute código al instalarse."""),
("""**Terminal 3 — el sitio:**

```bash
npm --prefix src/web run dev
```

Espera a ver `Local: http://localhost:5173/`.""",
"""**Terminal 3 — el sitio.** En cmd, macOS o Linux:

```bash
npm --prefix src/web run dev
```

En PowerShell:

```powershell
npm.cmd --prefix src\\web run dev
```

Espera a ver `Local: http://localhost:5173/`."""),
("""| Página en blanco tras actualizar el código | Faltan dependencias nuevas del sitio | `npm --prefix src/web install` otra vez |""",
"""| Página en blanco tras actualizar el código | Faltan dependencias nuevas del sitio | Repite el [paso 3 del camino B](#paso-3--prepara-el-sitio) |
| `"vite" no se reconoce como un comando interno o externo` | Faltan las dependencias del sitio: el paso 3 del camino B no se hizo, falló o se hizo en otra carpeta | Desde la carpeta del proyecto, repite el paso 3 y comprueba que existe `src\\web\\node_modules\\.bin\\vite.cmd`. Si no vas a cambiar la interfaz, puedes saltarte Node: la Channel sirve el sitio ya compilado con `--webroot` (camino C, paso 5) |
| `npm.ps1 no se puede cargar ... la ejecución de scripts está deshabilitada` | En PowerShell, `npm` es un script que la política bloquea | Escribe `npm.cmd` en lugar de `npm` |
| El `npm ci` termina con `ECONNRESET`, `ETIMEDOUT`, `E403`, `self-signed certificate` o `unable to get local issuer certificate` | La red de la empresa no deja llegar al registro de npm | Pide a soporte el proxy o el registro interno de npm. Mientras tanto, usa la Channel sirviendo el sitio con `--webroot` (camino C), que no necesita Node |
| Vite avisa que la versión de Node no es compatible, o `crypto.hash is not a function` | Node anterior a 22.12 (o a 20.19) | Instala Node 22.12 o superior y repite el paso 3 |"""),
])
