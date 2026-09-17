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

## Arrancar

1. Descomprimir el `.zip` completo en una carpeta local (no dentro de OneDrive sincronizado).
2. Doble clic en `start.cmd`. Se abre el navegador en `http://localhost:5100/`.
3. `Sign in` entra con el usuario de desarrollo, que tiene todos los permisos.
4. Para detener: Enter en la ventana de la consola.

Windows puede advertir que los ejecutables no están firmados (SmartScreen). El paquete de
prueba no se firma; la firma de binarios e imágenes es parte del hallazgo H-17.

Si la empresa **bloquea** los ejecutables sin firmar (no es un aviso: no abren), este
paquete no sirve en ese equipo tal cual. El camino C de la guía paso a paso levanta la
aplicación solo con `dotnet.exe` y usa de este `.zip` únicamente la carpeta
`channel\wwwroot`, que son archivos estáticos.

### Opciones

| Comando | Qué hace |
|---|---|
| `start.cmd` | 250 aplicaciones generadas en memoria (se pierden al detener) |
| `start.cmd -Data File` | Carga de archivos: subir los CSV de `samples\` desde la pantalla **Data**. Las cargas y las reglas publicadas quedan en `data\` |
| `start.cmd -Port 5200` | Otro puerto (la capa System usa el siguiente) |
| `start.cmd -NoBrowser` | No abre el navegador |

## Qué probar

- **Portfolio**: filtros desde los gráficos, navegación con el filtro puesto, **Download CSV**.
- **Cálculo** de una aplicación: `/applications/14381/calculation` debe dar **79,67**.
- **What-if** y **plan futuro** de una aplicación.
- **Roadmap** por dimensión y su **Download CSV**.
- **Data**: con `-Data File`, subir los ocho CSV de `samples\`; la tendencia aparece desde la
  segunda carga y marca los cambios de reglas.
- **Rules**: cambiar un peso, simular, **Compare your draft** con una aplicación, publicar.
  Tras publicar aparece el aviso de reglas nuevas.

## Seguridad del paquete

- Las dos capas escuchan solo en la interfaz local.
- El token de servicio entre capas se genera en cada arranque, viaja como archivo en una
  carpeta temporal (igual que con el gestor de secretos) y se borra al detener.
- La SPA se sirve con la misma CSP estricta que en despliegue y sin source maps.
- Sin símbolos de depuración en el paquete.

## Requisitos del equipo

| Variante | Requisito | Tamaño aproximado |
|---|---|---|
| `selfcontained` | Windows 10/11 x64 | 90–110 MB descomprimido |
| `frameworkdependent` | Runtime de ASP.NET Core 10 instalado | 15–25 MB descomprimido |

Puertos libres: 5100 y 5101 (o los que indique `-Port`).

## Generar el paquete

Desde la raíz del repositorio:

```powershell
pwsh tools/portable/build-portable.ps1
pwsh tools/portable/build-portable.ps1 -Variant FrameworkDependent
```

El `.zip` y su SHA-256 quedan en `artifacts/`. El último `.zip` se versiona en el repositorio;
la carpeta descomprimida no. Antes de confirmar uno nuevo, borrar el anterior: cada paquete
queda para siempre en la historia y GitHub rechaza archivos de más de 100 MB.
